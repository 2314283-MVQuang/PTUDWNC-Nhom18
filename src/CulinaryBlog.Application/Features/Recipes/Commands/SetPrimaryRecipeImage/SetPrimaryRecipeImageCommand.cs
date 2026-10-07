using CulinaryBlog.Application.Features.Recipes.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.SetPrimaryRecipeImage;

public record SetPrimaryRecipeImageCommand(
    Guid RecipeId,
    Guid ImageId) : IRequest<RecipeImageItemDto>;
