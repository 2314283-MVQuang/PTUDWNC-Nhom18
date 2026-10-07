using CulinaryBlog.Application.Features.Recipes.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeStep;

public record AddRecipeStepCommand(
    Guid RecipeId,
    string Title,
    string Description,
    int? StepNumber = null,
    int? TimerMinutes = null,
    string? ImageUrl = null) : IRequest<RecipeStepItemDto>;
