using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>
/// FR-JOB-002 (Tuần 7 — Tiến): Hangfire Background Job sinh bản Medium (800x600) và Thumbnail (300x300)
/// sau khi ảnh được upload, lưu lại MediumUrl và ThumbnailUrl vào CSDL.
/// </summary>
public class GenerateThumbnailJob(
    IServiceScopeFactory scopeFactory,
    IImageProcessor imageProcessor,
    IFileStorageService fileStorageService,
    ILogger<GenerateThumbnailJob> logger,
    IHttpClientFactory? httpClientFactory = null)
{
    public const int MediumMaxWidth = 800;
    public const int MediumMaxHeight = 600;
    public const int ThumbnailMaxWidth = 300;
    public const int ThumbnailMaxHeight = 300;

    public async Task ExecuteAsync(Guid imageOrFileId, CancellationToken ct = default)
    {
        logger.LogInformation("[GenerateThumbnailJob] Bắt đầu sinh thumbnail cho ID={Id}...", imageOrFileId);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        // 1. Tìm RecipeImage hoặc UploadedFile
        var recipeImage = await db.RecipeImages.FirstOrDefaultAsync(x => x.Id == imageOrFileId, ct);
        var uploadedFile = await db.UploadedFiles.FirstOrDefaultAsync(x => x.Id == imageOrFileId, ct);

        if (recipeImage == null && uploadedFile == null)
        {
            logger.LogWarning("[GenerateThumbnailJob] Không tìm thấy RecipeImage hoặc UploadedFile với Id={Id}", imageOrFileId);
            return;
        }

        var originalUrl = recipeImage?.OriginalUrl ?? uploadedFile?.Url ?? string.Empty;
        var storageKey = uploadedFile?.StorageKey;

        // Nếu có recipeImage mà chưa có storageKey, tìm thử trong UploadedFiles theo Url
        UploadedFile? matchingUploadedFile = uploadedFile;
        if (matchingUploadedFile == null && recipeImage != null && !string.IsNullOrWhiteSpace(originalUrl))
        {
            matchingUploadedFile = await db.UploadedFiles
                .FirstOrDefaultAsync(f => f.Url == originalUrl, ct);

            if (matchingUploadedFile != null)
            {
                storageKey = matchingUploadedFile.StorageKey;
            }
        }

        // Nếu vẫn chưa có storageKey, thử bóc tách từ originalUrl nếu chứa bucket name
        if (string.IsNullOrEmpty(storageKey) && !string.IsNullOrWhiteSpace(originalUrl))
        {
            var bucket = fileStorageService.BucketName;
            var bucketToken = $"/{bucket}/";
            var idx = originalUrl.IndexOf(bucketToken, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                storageKey = originalUrl[(idx + bucketToken.Length)..];
            }
        }

        // 2. Lấy dữ liệu file gốc (thử MinIO trước, fallback HTTP nếu có URL hợp lệ)
        byte[]? originalBytes = null;

        if (!string.IsNullOrEmpty(storageKey))
        {
            try
            {
                using var stream = await fileStorageService.GetFileStreamAsync(storageKey, ct);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                originalBytes = ms.ToArray();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[GenerateThumbnailJob] Không đọc được trực tiếp từ MinIO key={Key}", storageKey);
            }
        }

        if (originalBytes == null || originalBytes.Length == 0)
        {
            if (!string.IsNullOrWhiteSpace(originalUrl) && Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri))
            {
                try
                {
                    var httpClient = httpClientFactory?.CreateClient() ?? new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(15);
                    var response = await httpClient.GetAsync(uri, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        originalBytes = await response.Content.ReadAsByteArrayAsync(ct);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[GenerateThumbnailJob] Không tải được ảnh qua HTTP từ {Url}", originalUrl);
                }
            }
        }

        if (originalBytes == null || originalBytes.Length == 0)
        {
            logger.LogError("[GenerateThumbnailJob] Không thể lấy dữ liệu ảnh nguồn cho Id={Id}", imageOrFileId);
            return;
        }

        // 3. Thực hiện Resize bằng IImageProcessor
        byte[] mediumBytes;
        byte[] thumbBytes;
        try
        {
            mediumBytes = imageProcessor.Resize(originalBytes, MediumMaxWidth, MediumMaxHeight);
            thumbBytes = imageProcessor.Resize(originalBytes, ThumbnailMaxWidth, ThumbnailMaxHeight);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[GenerateThumbnailJob] Lỗi khi xử lý resize ảnh cho Id={Id}", imageOrFileId);
            throw;
        }

        // 4. Upload bản medium và thumb lên MinIO
        var baseKey = !string.IsNullOrEmpty(storageKey)
            ? (storageKey.Contains('.') ? storageKey[..storageKey.LastIndexOf('.')] : storageKey)
            : $"uploads/thumbnails/{imageOrFileId:N}";

        var ext = !string.IsNullOrEmpty(storageKey) && storageKey.Contains('.')
            ? storageKey[storageKey.LastIndexOf('.')..]
            : ".jpg";

        var mediumKey = $"{baseKey}_medium{ext}";
        var thumbKey = $"{baseKey}_thumb{ext}";

        string mediumUrl;
        string thumbnailUrl;

        try
        {
            using var mediumStream = new MemoryStream(mediumBytes);
            using var thumbStream = new MemoryStream(thumbBytes);

            mediumUrl = await fileStorageService.UploadFileAsync(mediumKey, mediumStream, "image/jpeg", ct);
            thumbnailUrl = await fileStorageService.UploadFileAsync(thumbKey, thumbStream, "image/jpeg", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[GenerateThumbnailJob] Không upload được lên MinIO, sử dụng public URL dự kiến");
            mediumUrl = fileStorageService.GetPublicUrl(mediumKey);
            thumbnailUrl = fileStorageService.GetPublicUrl(thumbKey);
        }

        // 5. Cập nhật vào Database
        if (recipeImage != null)
        {
            recipeImage.MediumUrl = mediumUrl;
            recipeImage.ThumbnailUrl = thumbnailUrl;
        }

        if (uploadedFile != null)
        {
            uploadedFile.MediumUrl = mediumUrl;
            uploadedFile.ThumbnailUrl = thumbnailUrl;
        }

        if (matchingUploadedFile != null && matchingUploadedFile.Id != uploadedFile?.Id)
        {
            matchingUploadedFile.MediumUrl = mediumUrl;
            matchingUploadedFile.ThumbnailUrl = thumbnailUrl;
        }

        await db.SaveChangesAsync(ct);

        // 6. Xoá cache nếu là ảnh của Recipe
        if (recipeImage != null)
        {
            var cacheInvalidator = scope.ServiceProvider.GetService<ICacheInvalidator>();
            if (cacheInvalidator != null)
            {
                await cacheInvalidator.EvictByTagAsync($"recipe-{recipeImage.RecipeId}", ct);
                await cacheInvalidator.EvictByTagAsync("recipes", ct);
            }
        }

        logger.LogInformation("[GenerateThumbnailJob] Sinh thumbnail thành công cho Id={Id}: Medium={Medium}, Thumb={Thumb}",
            imageOrFileId, mediumUrl, thumbnailUrl);
    }
}
