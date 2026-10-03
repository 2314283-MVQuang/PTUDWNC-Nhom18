using CulinaryBlog.Application.Features.Recipes.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeImage;

public record AddRecipeImageCommand(
    Guid RecipeId,
    string ImageUrl,
    string? AltText = null,
    bool IsPrimary = false,
    int? OrderIndex = null) : IRequest<RecipeImageDto>;
