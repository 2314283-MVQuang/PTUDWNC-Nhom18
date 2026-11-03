using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>
/// FR-JOB-002 (Tuần 7 — Tiến), SRS trang 38: sau khi upload ảnh công thức (FR-RCP-008), sinh bản
/// thumbnail 300×300 và medium 800×600 từ ảnh gốc, lưu cả lên MinIO rồi cập nhật
/// <c>MediumUrl</c>/<c>ThumbnailUrl</c>. Retry 3 lần; vẫn lỗi thì ảnh gốc tiếp tục hiển thị.
///
/// Chỉ đọc ảnh gốc từ bucket của hệ thống qua <see cref="IFileStorageService"/> — KHÔNG tải ảnh qua
/// HTTP theo URL do người dùng gửi (tránh SSRF: server bị lợi dụng gọi tới địa chỉ nội bộ).
/// </summary>
public class GenerateThumbnailJob(
    CulinaryBlogDbContext db,
    IImageProcessor imageProcessor,
    IFileStorageService fileStorageService,
    ILogger<GenerateThumbnailJob> logger,
    ICacheInvalidator? cacheInvalidator = null)
{
    public const int MediumMaxWidth = 800;
    public const int MediumMaxHeight = 600;
    public const int ThumbnailMaxWidth = 300;
    public const int ThumbnailMaxHeight = 300;

    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(Guid recipeImageId, CancellationToken ct = default)
    {
        var image = await db.RecipeImages.FirstOrDefaultAsync(x => x.Id == recipeImageId, ct);
        if (image is null)
        {
            // Ảnh đã bị xoá trước khi job chạy — không có gì để làm, không retry.
            logger.LogWarning("GenerateThumbnailJob: không tìm thấy ảnh {ImageId}, bỏ qua.", recipeImageId);
            return;
        }

        var storageKey = FileStorageObjectNameResolver.FromPublicUrl(image.OriginalUrl, fileStorageService.BucketName);
        if (storageKey is null)
        {
            logger.LogWarning(
                "GenerateThumbnailJob: ảnh {ImageId} không nằm trong bucket {Bucket} ({Url}), bỏ qua.",
                recipeImageId, fileStorageService.BucketName, image.OriginalUrl);
            return;
        }

        // Lỗi đọc/ghi MinIO được ném ra để Hangfire retry. Không ghi URL "dự kiến" khi upload thất
        // bại — URL trỏ tới file không tồn tại sẽ làm ảnh trên giao diện bị vỡ.
        byte[] originalBytes;
        await using (var source = await fileStorageService.GetFileStreamAsync(storageKey, ct))
        using (var buffer = new MemoryStream())
        {
            await source.CopyToAsync(buffer, ct);
            originalBytes = buffer.ToArray();
        }

        byte[] mediumBytes;
        byte[] thumbnailBytes;
        try
        {
            mediumBytes = imageProcessor.Resize(originalBytes, MediumMaxWidth, MediumMaxHeight);
            thumbnailBytes = imageProcessor.Resize(originalBytes, ThumbnailMaxWidth, ThumbnailMaxHeight);
        }
        catch (Exception ex)
        {
            // File không phải ảnh hợp lệ — thử lại cũng không khác, nên không retry.
            logger.LogError(ex, "GenerateThumbnailJob: không đọc được ảnh {Key} để resize, giữ ảnh gốc.", storageKey);
            return;
        }

        // Bản resize luôn lưu dạng JPEG nên đuôi file là .jpg (không giữ đuôi .png/.webp của ảnh gốc).
        var dot = storageKey.LastIndexOf('.');
        var baseKey = dot > storageKey.LastIndexOf('/') ? storageKey[..dot] : storageKey;
        var mediumKey = $"{baseKey}_medium.jpg";
        var thumbnailKey = $"{baseKey}_thumb.jpg";

        string mediumUrl;
        string thumbnailUrl;
        using (var mediumStream = new MemoryStream(mediumBytes))
        {
            mediumUrl = await fileStorageService.UploadFileAsync(mediumKey, mediumStream, "image/jpeg", ct);
        }
        using (var thumbnailStream = new MemoryStream(thumbnailBytes))
        {
            thumbnailUrl = await fileStorageService.UploadFileAsync(thumbnailKey, thumbnailStream, "image/jpeg", ct);
        }

        image.MediumUrl = mediumUrl;
        image.ThumbnailUrl = thumbnailUrl;

        // Metadata upload (FR-FILE-001) của cùng file gốc cũng ghi lại 2 bản resize.
        var uploadedFile = await db.UploadedFiles.FirstOrDefaultAsync(x => x.StorageKey == storageKey, ct);
        if (uploadedFile is not null)
        {
            uploadedFile.MediumUrl = mediumUrl;
            uploadedFile.ThumbnailUrl = thumbnailUrl;
        }

        await db.SaveChangesAsync(ct);

        // ICacheInvalidator đăng ký ở API (Output Cache); host không có Output Cache thì bỏ qua.
        if (cacheInvalidator is not null)
        {
            await cacheInvalidator.EvictByTagAsync($"recipe-{image.RecipeId}", ct);
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
        }

        logger.LogInformation(
            "GenerateThumbnailJob: ảnh {ImageId} có medium {MediumUrl} và thumbnail {ThumbnailUrl}.",
            recipeImageId, mediumUrl, thumbnailUrl);
    }
}
