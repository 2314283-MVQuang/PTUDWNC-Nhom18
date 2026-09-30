using CulinaryBlog.Application.Features.Recipes.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeBySlug;

public sealed record GetRecipeBySlugQuery(
    string Slug) : IRequest<RecipeDetailDto>;