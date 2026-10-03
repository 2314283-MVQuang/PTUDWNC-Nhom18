using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeStep;

public record DeleteRecipeStepCommand(
    Guid RecipeId,
    Guid StepId) : IRequest;
