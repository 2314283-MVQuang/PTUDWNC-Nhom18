using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeById;

public sealed class GetRecipeByIdQueryHandler(
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetRecipeByIdQuery, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(
        GetRecipeByIdQuery request,
        CancellationToken ct)
    {
        var recipe =
            await recipeRepository.GetByIdWithDetailsAsync(
                request.Id,
                ct);

        if (recipe is null)
        {
            throw new RecipeNotFoundException(
                request.Id);
        }

        EnsureCanRead(
            recipe.Status,
            recipe.AuthorId);

        return recipe.ToDetailDto();
    }

    private void EnsureCanRead(
        RecipeStatus status,
        string authorId)
    {
        // Published:
        // public có thể xem.
        if (status == RecipeStatus.Published)
        {
            return;
        }

        // Draft / Archived:
        // Admin hoặc chủ sở hữu mới được xem.
        if (currentUser.IsInRole("Admin") ||
            (!string.IsNullOrWhiteSpace(currentUser.UserId) &&
             currentUser.UserId == authorId))
        {
            return;
        }

        throw new ForbiddenAccessException(
            "Bạn không có quyền xem công thức này.");
    }
}