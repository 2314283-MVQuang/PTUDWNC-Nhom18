using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

/// <summary>
/// Handler xử lý SearchRecipesQuery (FR-SRCH-001 & FR-SRCH-004).
/// </summary>
public sealed class SearchRecipesQueryHandler(IRecipeRepository recipeRepository)
    : IRequestHandler<SearchRecipesQuery, PagedResult<RecipeSummaryDto>>
{
    public async Task<PagedResult<RecipeSummaryDto>> Handle(
        SearchRecipesQuery request,
        CancellationToken cancellationToken)
    {
        var (recipes, totalCount) = await recipeRepository.SearchRecipesPagedAsync(
            keyword: request.Keyword,
            page: request.Page,
            pageSize: request.PageSize,
            categoryId: request.CategoryId,
            difficulty: request.Difficulty,
            maxCookTime: request.MaxCookTime,
            minServings: request.MinServings,
            sort: request.Sort,
            ct: cancellationToken);

        var items = recipes
            .Select(x => x.ToSummaryDto())
            .ToList();

        return PagedResult<RecipeSummaryDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }
}