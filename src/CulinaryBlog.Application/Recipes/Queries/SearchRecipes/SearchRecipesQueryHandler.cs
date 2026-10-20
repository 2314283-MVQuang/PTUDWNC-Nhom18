using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Application.Recipes.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

public class SearchRecipesQueryHandler(IRecipeRepository recipeRepository)
    : IRequestHandler<SearchRecipesQuery, IEnumerable<RecipeDto>>
{
    public async Task<IEnumerable<RecipeDto>> Handle(
        SearchRecipesQuery request,
        CancellationToken cancellationToken)
    {
        // FR-SRCH-003: sort sai -> InvalidSortException (400) trước khi chạm database.
        var sort = SortParser.Parse(request.Sort);
        var filters = new RecipeSearchFilters(request.CategoryId, request.Difficulty,
            request.MinPrepTime, request.MaxPrepTime, request.MinCookTime, request.MaxCookTime,
            request.MinServings);

        // Sắp xếp được làm NGAY TRONG SQL (ORDER BY trước LIMIT) — nếu sắp xếp trên danh sách đã cắt
        // 50 dòng theo độ liên quan thì "-createdAt" sẽ không trả đúng các món mới nhất.
        var recipes = await recipeRepository.SearchRecipesAsync(
            request.Keyword, cancellationToken, filters, sort);

        return recipes.Select(recipe => new RecipeDto(
            recipe.Id,
            recipe.Slug,
            recipe.Title,
            recipe.Description,
            recipe.PrepTime,
            recipe.CookTime,
            recipe.Servings,
            recipe.Difficulty,
            recipe.Status,
            recipe.PublishedAt));
    }
}
