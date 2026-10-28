using CulinaryBlog.Application.Common.Authorization;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UnpublishRecipe;

/// <summary>FR-RCP-005 (Tuần 8 — Ý): PATCH /recipes/{id}/unpublish [Author-Owner/Admin].</summary>
public sealed record UnpublishRecipeCommand(Guid RecipeId) : IRequest<RecipeDetailDto>;

public sealed class UnpublishRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ICacheInvalidator cacheInvalidator)
    : IRequestHandler<UnpublishRecipeCommand, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(UnpublishRecipeCommand request, CancellationToken ct)
    {
        var recipe = await recipeRepository.GetByIdAsync(request.RecipeId, ct)
            ?? throw new RecipeNotFoundException(request.RecipeId);

        RecipeAuthorization.EnsureCanModify(recipe, currentUser);

        if (recipe.Status == RecipeStatus.Published)
        {
            recipe.Unpublish();
            try
            {
                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new RecipeConcurrencyConflictException();
            }

            // Công thức biến mất khỏi danh sách, chi tiết công khai, tìm kiếm và số đếm danh mục.
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
            await cacheInvalidator.EvictByTagAsync($"recipe-{recipe.Id}", ct);
            await cacheInvalidator.EvictByTagAsync("search", ct);
            await cacheInvalidator.EvictByTagAsync("categories", ct);
        }

        var updated = await recipeRepository.GetByIdWithDetailsAsync(recipe.Id, ct)
            ?? throw new RecipeNotFoundException(recipe.Id);
        return updated.ToDetailDto();
    }
}
