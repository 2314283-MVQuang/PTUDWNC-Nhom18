using CulinaryBlog.Application.Features.Recipes.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipeStep;

public record UpdateRecipeStepCommand(
    Guid RecipeId,
    Guid StepId,
    string Title,
    string Description,
    int? StepNumber = null,
    int? TimerMinutes = null,
    string? ImageUrl = null) : IRequest<RecipeStepDto>;
