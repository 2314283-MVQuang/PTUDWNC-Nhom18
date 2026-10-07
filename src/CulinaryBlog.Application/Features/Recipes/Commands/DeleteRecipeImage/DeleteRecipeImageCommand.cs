using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeImage;

public record DeleteRecipeImageCommand(
    Guid RecipeId,
    Guid ImageId) : IRequest;
