using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.SetPrimaryRecipeImage;

public class SetPrimaryRecipeImageCommandHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeImage> images,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICacheInvalidator? cacheInvalidator = null)
    : IRequestHandler<SetPrimaryRecipeImageCommand, RecipeImageItemDto>
{
    public async Task<RecipeImageItemDto> Handle(SetPrimaryRecipeImageCommand request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        if (currentUser.UserId != recipe.AuthorId && !currentUser.IsInRole("Admin"))
        {
            throw new ForbiddenAccessException("Bạn không có quyền chỉnh sửa ảnh của công thức này.");
        }

        var allImages = await images.Query()
            .Where(img => img.RecipeId == request.RecipeId)
            .ToListAsync(ct);

        var targetImage = allImages.FirstOrDefault(i => i.Id == request.ImageId);
        if (targetImage is null)
        {
            throw new NotFoundException(nameof(RecipeImage), request.ImageId);
        }

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Tắt cờ IsPrimary của tất cả các ảnh khác
            foreach (var img in allImages.Where(i => i.Id != targetImage.Id && i.IsPrimary))
            {
                img.IsPrimary = false;
                images.Update(img);
            }
            await unitOfWork.SaveChangesAsync(ct);

            // Bật cờ IsPrimary của ảnh được chọn
            targetImage.IsPrimary = true;
            images.Update(targetImage);
            await unitOfWork.SaveChangesAsync(ct);
        }, ct);

        if (cacheInvalidator is not null)
        {
            await cacheInvalidator.EvictByTagAsync($"recipe-{request.RecipeId}", ct);
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
        }

        return new RecipeImageItemDto(
            targetImage.Id,
            targetImage.RecipeId,
            targetImage.OriginalUrl,
            targetImage.MediumUrl,
            targetImage.ThumbnailUrl,
            targetImage.AltText,
            targetImage.IsPrimary,
            targetImage.OrderIndex);
    }
}
