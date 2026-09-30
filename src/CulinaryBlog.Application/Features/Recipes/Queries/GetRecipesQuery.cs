using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Enums;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipes;

/// <summary>
/// FR-RCP-001.
/// GET /api/v1/recipes
/// </summary>
public sealed record GetRecipesQuery(
    int Page = 1,
    int PageSize = 12,
    Guid? CategoryId = null,
    RecipeDifficulty? Difficulty = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string? Sort = "-createdAt")
    : IRequest<PagedResult<RecipeSummaryDto>>;