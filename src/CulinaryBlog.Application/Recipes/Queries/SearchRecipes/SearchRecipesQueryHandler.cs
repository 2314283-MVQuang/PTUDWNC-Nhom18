using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

public class SearchRecipesQueryHandler(IRecipeRepository recipeRepository)
    : IRequestHandler<SearchRecipesQuery, PagedResult<RecipeSummaryDto>>
{
    public async Task<PagedResult<RecipeSummaryDto>> Handle(
        SearchRecipesQuery request,
        CancellationToken cancellationToken)
    {
        // FR-SRCH-003: sort sai -> InvalidSortException (400) trước khi chạm database.
        var sort = SortParser.Parse(request.Sort);
        var filters = new RecipeSearchFilters(request.CategoryId, request.Difficulty,
            request.MinPrepTime, request.MaxPrepTime, request.MinCookTime, request.MaxCookTime,
            request.MinServings);

        // Lọc -> đếm tổng -> sắp xếp -> OFFSET/LIMIT đều chạy trong SQL (FR-SRCH-004), nên
        // totalCount là số kết quả thật sau lọc, không còn giới hạn cứng 50 dòng như trước.
        var (recipes, totalCount) = await recipeRepository.SearchRecipesAsync(
            request.Keyword, filters, sort, request.Page, request.PageSize, cancellationToken);

        return PagedResult<RecipeSummaryDto>.Create(
            recipes.Select(recipe => recipe.ToSummaryDto()).ToList(),
            request.Page,
            request.PageSize,
            totalCount);
    }
}
