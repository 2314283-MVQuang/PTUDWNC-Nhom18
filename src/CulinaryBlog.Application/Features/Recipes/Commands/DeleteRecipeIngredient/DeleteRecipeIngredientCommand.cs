using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeIngredient;

public record DeleteRecipeIngredientCommand(
    Guid RecipeId,
    Guid IngredientId) : IRequest;
