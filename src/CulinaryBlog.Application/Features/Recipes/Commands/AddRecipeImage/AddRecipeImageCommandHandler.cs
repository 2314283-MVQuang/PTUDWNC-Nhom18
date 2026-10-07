using CulinaryBlog.Application.Common.Exceptions;
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
    ICacheInvalidator? cacheInvalidator = null)
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

    private static string? NullIfBlank(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
