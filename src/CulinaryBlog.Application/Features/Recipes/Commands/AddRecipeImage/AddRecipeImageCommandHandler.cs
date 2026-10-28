using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Features.Files.Commands.GeneratePresignedUrl;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeImage;

public class AddRecipeImageCommandHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeImage> images,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IFileStorageService fileStorageService,
    IUploadedFileRepository uploadedFiles,
    ICacheInvalidator? cacheInvalidator = null,
    IThumbnailJobScheduler? thumbnailJobScheduler = null)
    : IRequestHandler<AddRecipeImageCommand, RecipeImageItemDto>
{
    public async Task<RecipeImageItemDto> Handle(AddRecipeImageCommand request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        if (currentUser.UserId != recipe.AuthorId && !currentUser.IsInRole("Admin"))
        {
            throw new ForbiddenAccessException("Bạn không có quyền thêm ảnh cho công thức này.");
        }

        await EnsureUploadedImageAsync(request.ImageUrl, ct);

        var existingImages = await images.Query()
            .Where(img => img.RecipeId == request.RecipeId)
            .ToListAsync(ct);

        bool shouldBePrimary = request.IsPrimary || existingImages.Count == 0;

        int orderIndex;
        if (request.OrderIndex.HasValue)
        {
            orderIndex = request.OrderIndex.Value;
        }
        else
        {
            var maxOrder = existingImages.Count > 0 ? existingImages.Max(i => i.OrderIndex) : -1;
            orderIndex = maxOrder + 1;
        }

        var newImage = new RecipeImage
        {
            Id = Guid.NewGuid(),
            RecipeId = request.RecipeId,
            OriginalUrl = request.ImageUrl.Trim(),
            AltText = NullIfBlank(request.AltText),
            IsPrimary = shouldBePrimary,
            OrderIndex = orderIndex,
        };

        if (shouldBePrimary && existingImages.Count > 0)
        {
            // Tắt cờ IsPrimary của các ảnh cũ để thỏa mãn unique partial index
            foreach (var img in existingImages.Where(i => i.IsPrimary))
            {
                img.IsPrimary = false;
                images.Update(img);
            }
        }

        await images.AddAsync(newImage, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // FR-JOB-002: Enqueue background job sinh Medium và Thumbnail qua Hangfire
        thumbnailJobScheduler?.EnqueueThumbnailGeneration(newImage.Id);

        if (cacheInvalidator is not null)
        {
            await cacheInvalidator.EvictByTagAsync($"recipe-{request.RecipeId}", ct);
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
        }

        return new RecipeImageItemDto(
            newImage.Id,
            newImage.RecipeId,
            newImage.OriginalUrl,
            newImage.MediumUrl,
            newImage.ThumbnailUrl,
            newImage.AltText,
            newImage.IsPrimary,
            newImage.OrderIndex);
    }

    /// <summary>
    /// Tuần 8 (Thịnh) — FR-RCP-008/FR-FILE-001: chỉ nhận ảnh người dùng vừa tải lên MinIO của hệ
    /// thống qua POST /files/presigned-url:
    ///   1. URL phải nằm trong bucket của hệ thống (không nhận link ảnh ngoài);
    ///   2. có bản ghi UploadedFiles và do CHÍNH người này tải lên (hoặc Admin);
    ///   3. nội dung thật là ảnh JPEG/PNG/WebP/AVIF (magic bytes) và không vượt 5MB — presigned PUT
    ///      không kiểm được nội dung nên kiểm ở đây; file sai bị xoá khỏi bucket.
    /// </summary>
    private async Task EnsureUploadedImageAsync(string imageUrl, CancellationToken ct)
    {
        var storageKey = FileStorageObjectNameResolver.FromPublicUrl(imageUrl?.Trim(), fileStorageService.BucketName);
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
            throw new InvalidFileException(detected is null
                ? "File không hợp lệ: nội dung không phải ảnh JPEG, PNG, WebP hoặc AVIF."
                : "Kích thước file vượt quá giới hạn 5MB.");
        }
    }

    private static string? NullIfBlank(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
