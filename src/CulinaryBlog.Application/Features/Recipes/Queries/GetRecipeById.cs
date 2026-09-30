using CulinaryBlog.Application.Features.Recipes.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeById;

/// <summary>
/// FR-RCP-002.
/// GET /api/v1/recipes/{id}
/// </summary>
public sealed record GetRecipeByIdQuery(
    Guid Id) : IRequest<RecipeDetailDto>;