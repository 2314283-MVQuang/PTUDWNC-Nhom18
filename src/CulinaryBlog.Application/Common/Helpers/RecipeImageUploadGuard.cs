using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Files.Commands.GeneratePresignedUrl;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Common.Helpers;

/// <summary>
/// Kiểm tra ảnh trước khi gắn — dùng chung cho ảnh công thức (FR-RCP-008), ảnh minh hoạ từng bước
/// thực hiện (FR-RCP-010) và ảnh danh mục (FR-CAT-003/004) — Tuần 10, Quang.
///
/// Một URL ảnh chỉ được gắn khi:
///   1. nằm trong bucket của hệ thống, có bản ghi UploadedFiles và do CHÍNH người này tải lên
///      (Admin được dùng ảnh của người khác) — không nhận link ảnh ngoài;
///   2. nội dung thật là JPEG/PNG/WebP/AVIF (magic bytes) và ≤ 5MB — presigned PUT không kiểm
///      được nội dung nên kiểm ở đây; file sai bị xoá khỏi bucket;
///   3. chưa gắn vào ảnh công thức, bước hay danh mục nào khác (kể cả bản ghi đã xoá mềm) — nếu
///      không, xoá ở một chỗ (FR-FILE-002 xoá file trên MinIO) sẽ làm hỏng ảnh ở chỗ kia. Muốn dùng
///      lại một ảnh cũ ("Thư viện ảnh của tôi"), frontend tải lên một BẢN SAO mới.
/// </summary>
public sealed class RecipeImageUploadGuard(
    IFileStorageService fileStorageService,
    IUploadedFileRepository uploadedFiles,
    IRepository<RecipeImage> images,
    IRepository<RecipeStep> steps,
    IRepository<Category> categories,
    ICurrentUser currentUser)
{
    public const string AlreadyAttachedMessage =
        "Ảnh này đã được gắn vào một công thức. Vui lòng tải ảnh lên lại để dùng cho công thức khác.";

    /// <param name="exceptStepId">Bước đang được sửa — không tính chính nó là "đã dùng".</param>
    /// <param name="exceptCategoryId">Danh mục đang được sửa — tương tự.</param>
    public async Task EnsureCanAttachAsync(
        string imageUrl, Guid? exceptStepId, CancellationToken ct, Guid? exceptCategoryId = null)
    {
        var trimmed = imageUrl.Trim();
        await EnsureUploadedImageAsync(trimmed, ct);

        if (await IsAttachedAsync(trimmed, exceptStepId, ct, exceptCategoryId))
        {
            throw new ConflictException(AlreadyAttachedMessage);
        }
    }

    /// <summary>URL đã được ảnh công thức, bước hay danh mục nào khác (trừ chỗ đang sửa) dùng chưa.</summary>
    public async Task<bool> IsAttachedAsync(
        string imageUrl, Guid? exceptStepId, CancellationToken ct, Guid? exceptCategoryId = null)
    {
        var usedByRecipeImage = await images.Query()
            .IgnoreQueryFilters()
            .AnyAsync(img => img.OriginalUrl == imageUrl, ct);
        if (usedByRecipeImage)
        {
            return true;
        }

        var usedByStep = await steps.Query()
            .IgnoreQueryFilters()
            .AnyAsync(s => s.ImageUrl == imageUrl && (exceptStepId == null || s.Id != exceptStepId), ct);
        if (usedByStep)
        {
            return true;
        }

        return await categories.Query()
            .IgnoreQueryFilters()
            .AnyAsync(c => c.ImageUrl == imageUrl && (exceptCategoryId == null || c.Id != exceptCategoryId), ct);
    }

    /// <summary>
    /// Object MinIO cần xoá khi ảnh của một bước/danh mục bị thay, gỡ hoặc bước bị xoá: chỉ file nằm
    /// trong bucket của hệ thống và không còn chỗ nào khác dùng. Link ngoài (dữ liệu cũ) bỏ qua.
    /// </summary>
    public async Task<IReadOnlyList<string>> ObjectNamesToDeleteAsync(
        string? oldImageUrl, Guid? exceptStepId, CancellationToken ct, Guid? exceptCategoryId = null)
    {
        var objectName = FileStorageObjectNameResolver.FromPublicUrl(oldImageUrl, fileStorageService.BucketName);
        if (objectName is null || await IsAttachedAsync(oldImageUrl!, exceptStepId, ct, exceptCategoryId))
        {
            return [];
        }

        return [objectName];
    }

    private async Task EnsureUploadedImageAsync(string imageUrl, CancellationToken ct)
    {
        var storageKey = FileStorageObjectNameResolver.FromPublicUrl(imageUrl, fileStorageService.BucketName);
        var uploaded = storageKey is null ? null : await uploadedFiles.GetByStorageKeyAsync(storageKey, ct);
        if (storageKey is null || uploaded is null)
        {
            throw new InvalidFileException("Ảnh phải được tải lên qua hệ thống (POST /api/v1/files/presigned-url).");
        }

        if (!currentUser.IsInRole("Admin") && !string.Equals(uploaded.UploadedBy, currentUser.UserId, StringComparison.Ordinal))
        {
            throw new ForbiddenAccessException("Bạn không thể dùng ảnh do người khác tải lên.");
        }

        byte[] content;
        try
        {
            await using var stream = await fileStorageService.GetFileStreamAsync(storageKey, ct);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            content = buffer.ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidFileException("Không tìm thấy file ảnh trên kho lưu trữ. Vui lòng tải ảnh lên lại.");
        }

        var detected = ImageFileSignature.Detect(content.AsSpan(0, Math.Min(content.Length, ImageFileSignature.HeaderLength)));
        if (detected is null || content.Length > GeneratePresignedUrlCommandHandler.MaxFileSizeBytes)
        {
            await fileStorageService.DeleteAsync(storageKey, ct);
            // File đã xoá khỏi bucket → ẩn khỏi "Thư viện ảnh của tôi".
            uploaded.IsDeleted = true;
            await uploadedFiles.SaveChangesAsync(ct);
            throw new InvalidFileException(detected is null
                ? "File không hợp lệ: nội dung không phải ảnh JPEG, PNG, WebP hoặc AVIF."
                : "Kích thước file vượt quá giới hạn 5MB.");
        }
    }
}
