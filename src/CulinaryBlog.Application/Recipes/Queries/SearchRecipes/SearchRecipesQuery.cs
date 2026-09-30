using CulinaryBlog.Application.Recipes.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

public record SearchRecipesQuery(string Keyword) : IRequest<IEnumerable<RecipeDto>>;