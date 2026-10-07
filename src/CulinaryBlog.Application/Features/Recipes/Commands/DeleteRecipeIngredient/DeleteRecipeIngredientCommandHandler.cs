using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeIngredient;

public class DeleteRecipeIngredientCommandHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeIngredient> ingredients,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICacheInvalidator? cacheInvalidator = null)
    : IRequestHandler<DeleteRecipeIngredientCommand>
{
    public async Task Handle(DeleteRecipeIngredientCommand request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        if (currentUser.UserId != recipe.AuthorId && !currentUser.IsInRole("Admin"))
        {
            throw new ForbiddenAccessException("Bạn không có quyền xóa nguyên liệu của công thức này.");
        }

        var ingredient = await ingredients.Query()
            .FirstOrDefaultAsync(i => i.Id == request.IngredientId && i.RecipeId == request.RecipeId, ct);

        if (ingredient is null)
        {
            throw new NotFoundException(nameof(RecipeIngredient), request.IngredientId);
        }

        // Soft delete: gán IsDeleted = true
        ingredient.IsDeleted = true;
        ingredients.Update(ingredient);
        await unitOfWork.SaveChangesAsync(ct);

        if (cacheInvalidator is not null)
        {
            await cacheInvalidator.EvictByTagAsync($"recipe-{request.RecipeId}", ct);
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
        }
    }
}
