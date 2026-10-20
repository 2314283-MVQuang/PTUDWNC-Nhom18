using CulinaryBlog.Application.Recipes.Dtos;
using MediatR;
using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

public record SearchRecipesQuery(
    string Keyword,
    Guid? CategoryId = null,
    RecipeDifficulty? Difficulty = null,
    int? MinPrepTime = null,
    int? MaxPrepTime = null,
    int? MinCookTime = null,
    int? MaxCookTime = null,
    int? MinServings = null) : IRequest<IEnumerable<RecipeDto>>;
