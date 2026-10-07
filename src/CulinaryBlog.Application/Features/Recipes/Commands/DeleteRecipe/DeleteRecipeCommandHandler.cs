using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe;

public class DeleteRecipeCommandHandler(
    IRecipeRepository recipes,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteRecipeCommand>
{
    public async Task Handle(DeleteRecipeCommand request, CancellationToken ct)
    {
        var recipe = await recipes.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("công thức", request.Id);

        if (!currentUser.IsInRole("Admin") && recipe.AuthorId != currentUser.UserId)
        {
            throw new ForbiddenAccessException();
        }

        recipe.IsDeleted = true;
        recipe.DeletedAt = DateTimeOffset.UtcNow;

        await unitOfWork.SaveChangesAsync(ct);
    }
}
