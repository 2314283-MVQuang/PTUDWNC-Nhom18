using CulinaryBlog.Application.Common.Authorization;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.PublishRecipe;

public sealed class PublishRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ICacheInvalidator cacheInvalidator)
    : IRequestHandler<PublishRecipeCommand, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(
        PublishRecipeCommand request,
        CancellationToken ct)
    {
        // 1. Lấy Recipe đang tracked
        var recipe = await recipeRepository.GetByIdForPublishingAsync(
            request.RecipeId, ct);

        if (recipe is null)
        {
            throw new RecipeNotFoundException(request.RecipeId);
        }

        // 2. Kiểm tra quyền Author/Admin
        // Admin -> được publish bất kỳ Recipe nào.
        // Author -> chỉ được publish Recipe của chính mình.
        RecipeAuthorization.EnsureCanModify(recipe, currentUser);

        // 3. Business rule trong Domain; gọi lại không làm đổi PublishedAt/RowVersion.
        var alreadyPublished = recipe.Status == RecipeStatus.Published;
        recipe.Publish();

        if (!alreadyPublished)
        {
            try
            {
                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new RecipeConcurrencyConflictException();
            }

            // Search có tag riêng; Category list chứa số công thức đã xuất bản.
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
            await cacheInvalidator.EvictByTagAsync($"recipe-{recipe.Id}", ct);
            await cacheInvalidator.EvictByTagAsync("search", ct);
            await cacheInvalidator.EvictByTagAsync("categories", ct);
        }

        // 6. Đọc lại để lấy navigation + rowVersion mới
        var publishedRecipe = await recipeRepository.GetByIdWithDetailsAsync(
            recipe.Id, ct);

        if (publishedRecipe is null)
        {
            throw new RecipeNotFoundException(recipe.Id);
        }

        return publishedRecipe.ToDetailDto();
    }
}
