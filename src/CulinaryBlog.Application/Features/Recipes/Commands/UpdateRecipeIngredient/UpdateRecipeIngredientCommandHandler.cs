using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipeIngredient;

public class UpdateRecipeIngredientCommandHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeIngredient> ingredients,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICacheInvalidator? cacheInvalidator = null)
    : IRequestHandler<UpdateRecipeIngredientCommand, RecipeIngredientItemDto>
{
    public async Task<RecipeIngredientItemDto> Handle(UpdateRecipeIngredientCommand request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        if (currentUser.UserId != recipe.AuthorId && !currentUser.IsInRole("Admin"))
        {
            throw new ForbiddenAccessException("Bạn không có quyền chỉnh sửa nguyên liệu của công thức này.");
        }

        var ingredient = await ingredients.Query()
            .FirstOrDefaultAsync(i => i.Id == request.IngredientId && i.RecipeId == request.RecipeId, ct);

        if (ingredient is null)
        {
            throw new NotFoundException(nameof(RecipeIngredient), request.IngredientId);
        }

        ingredient.Name = request.Name.Trim();
        ingredient.Quantity = request.Quantity;
        ingredient.Unit = NullIfBlank(request.Unit);
        ingredient.Notes = NullIfBlank(request.Notes);
        if (request.OrderIndex.HasValue)
        {
            ingredient.OrderIndex = request.OrderIndex.Value;
        }

        ingredients.Update(ingredient);
        await unitOfWork.SaveChangesAsync(ct);

        if (cacheInvalidator is not null)
        {
            await cacheInvalidator.EvictByTagAsync($"recipe-{request.RecipeId}", ct);
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
        }

        return new RecipeIngredientItemDto(
            ingredient.Id,
            ingredient.RecipeId,
            ingredient.Name,
            ingredient.Quantity,
            ingredient.Unit,
            ingredient.Notes,
            ingredient.OrderIndex);
    }

    private static string? NullIfBlank(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
