using CulinaryBlog.Application.Features.Recipes.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.PublishRecipe;

public sealed record PublishRecipeCommand(
    Guid RecipeId)
    : IRequest<RecipeDetailDto>;