using CulinaryBlog.Application.Common.Authorization;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeIngredients;

public record GetRecipeIngredientsQuery(Guid RecipeId) : IRequest<List<RecipeIngredientItemDto>>;

public class GetRecipeIngredientsQueryHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeIngredient> ingredients,
    ICurrentUser currentUser)
    : IRequestHandler<GetRecipeIngredientsQuery, List<RecipeIngredientItemDto>>
{
    public async Task<List<RecipeIngredientItemDto>> Handle(GetRecipeIngredientsQuery request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .Where(r => r.Id == request.RecipeId)
            .Select(r => new { r.Status, r.AuthorId })
            .FirstOrDefaultAsync(ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        // Bản nháp/lưu trữ: chỉ tác giả hoặc Admin xem được phần con (giống GET /recipes/{id}).
        RecipeAuthorization.EnsureCanRead(recipe.Status, recipe.AuthorId, currentUser);

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
