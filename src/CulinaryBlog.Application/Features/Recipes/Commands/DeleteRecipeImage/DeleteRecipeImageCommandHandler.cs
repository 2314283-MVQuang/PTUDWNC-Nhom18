using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeImage;

public class DeleteRecipeImageCommandHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeImage> images,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IFileStorageService fileStorageService,
    IFileDeletionScheduler fileDeletionScheduler,
    ICacheInvalidator? cacheInvalidator = null)
    : IRequestHandler<DeleteRecipeImageCommand>
{
    public async Task Handle(DeleteRecipeImageCommand request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        if (currentUser.UserId != recipe.AuthorId && !currentUser.IsInRole("Admin"))
        {
            throw new ForbiddenAccessException("Bạn không có quyền xóa ảnh của công thức này.");
        }

        var image = await images.Query()
            .FirstOrDefaultAsync(i => i.Id == request.ImageId && i.RecipeId == request.RecipeId, ct);

        if (image is null)
        {
            throw new NotFoundException(nameof(RecipeImage), request.ImageId);
        }

        // FR-FILE-002: lấy object name của cả 3 bản ảnh TRƯỚC khi đánh dấu xoá, nhưng chỉ lên lịch xoá
        // file SAU khi lưu DB thành công (SRS FR-RCP-008 bước 13–14) — lưu DB lỗi thì file vẫn còn.
        var objectNames = FileStorageObjectNameResolver.FromImageUrls(
            fileStorageService.BucketName, image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl);

        bool wasPrimary = image.IsPrimary;
        image.IsDeleted = true;
        image.IsPrimary = false;
        images.Update(image);

        if (wasPrimary)
        {
            var nextImage = await images.Query()
                .Where(i => i.RecipeId == request.RecipeId && i.Id != request.ImageId)
                .OrderBy(i => i.OrderIndex)
                .FirstOrDefaultAsync(ct);

            if (nextImage is not null)
            {
                nextImage.IsPrimary = true;
                images.Update(nextImage);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);

        // Hangfire fire-and-forget, retry 3 lần: MinIO tạm lỗi không làm request xoá ảnh trả 500.
        fileDeletionScheduler.Enqueue(objectNames);

        if (cacheInvalidator is not null)
        {
            await cacheInvalidator.EvictByTagAsync($"recipe-{request.RecipeId}", ct);
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
        }
    }
}
