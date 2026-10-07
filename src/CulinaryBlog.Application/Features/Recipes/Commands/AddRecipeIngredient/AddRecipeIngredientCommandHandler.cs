using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeIngredient;

public class AddRecipeIngredientCommandHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeIngredient> ingredients,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICacheInvalidator? cacheInvalidator = null)
    : IRequestHandler<AddRecipeIngredientCommand, RecipeIngredientItemDto>
{
    public async Task<RecipeIngredientItemDto> Handle(AddRecipeIngredientCommand request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        if (currentUser.UserId != recipe.AuthorId && !currentUser.IsInRole("Admin"))
        {
            throw new ForbiddenAccessException("Bạn không có quyền thêm nguyên liệu cho công thức này.");
        }

        int orderIndex;
        if (request.OrderIndex.HasValue)
        {
            orderIndex = request.OrderIndex.Value;
        }
        else
        {
            var maxOrder = await ingredients.Query()
                .Where(i => i.RecipeId == request.RecipeId)
                .MaxAsync(i => (int?)i.OrderIndex, ct) ?? -1;
            orderIndex = maxOrder + 1;
        }

        var ingredient = new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = request.RecipeId,
            Name = request.Name.Trim(),
            Quantity = request.Quantity,
            Unit = NullIfBlank(request.Unit),
            Notes = NullIfBlank(request.Notes),
            OrderIndex = orderIndex,
        };

        await ingredients.AddAsync(ingredient, ct);
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
