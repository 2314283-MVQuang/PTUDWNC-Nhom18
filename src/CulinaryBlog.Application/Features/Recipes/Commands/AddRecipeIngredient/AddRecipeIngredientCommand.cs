using CulinaryBlog.Application.Features.Recipes.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeIngredient;

public record AddRecipeIngredientCommand(
    Guid RecipeId,
    string Name,
    decimal? Quantity = null,
    string? Unit = null,
    string? Notes = null,
    int? OrderIndex = null) : IRequest<RecipeIngredientDto>;
