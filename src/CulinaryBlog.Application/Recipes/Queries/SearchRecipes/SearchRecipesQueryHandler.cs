using CulinaryBlog.Application.Common.Interfaces;
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
        var recipes = await recipeRepository.SearchRecipesAsync(request.Keyword, cancellationToken);

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