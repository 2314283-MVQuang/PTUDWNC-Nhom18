using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeIngredients;

public record GetRecipeIngredientsQuery(Guid RecipeId) : IRequest<List<RecipeIngredientItemDto>>;

public class GetRecipeIngredientsQueryHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeIngredient> ingredients)
    : IRequestHandler<GetRecipeIngredientsQuery, List<RecipeIngredientItemDto>>
{
    public async Task<List<RecipeIngredientItemDto>> Handle(GetRecipeIngredientsQuery request, CancellationToken ct)
    {
        var recipeExists = await recipes.Query()
            .AnyAsync(r => r.Id == request.RecipeId, ct);

        if (!recipeExists)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        var list = await ingredients.Query()
            .Where(i => i.RecipeId == request.RecipeId)
            .OrderBy(i => i.OrderIndex)
            .ThenBy(i => i.Name)
            .Select(i => new RecipeIngredientItemDto(
                i.Id,
                i.RecipeId,
                i.Name,
                i.Quantity,
                i.Unit,
                i.Notes,
                i.OrderIndex))
            .ToListAsync(ct);

        return list;
    }
}
