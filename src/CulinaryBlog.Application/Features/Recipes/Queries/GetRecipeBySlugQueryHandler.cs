using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using RecipeRepository = CulinaryBlog.Domain.Interfaces.IRecipeRepository;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeBySlug;

public sealed class GetRecipeBySlugQueryHandler(
    RecipeRepository recipeRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetRecipeBySlugQuery, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(
        GetRecipeBySlugQuery request,
        CancellationToken ct)
    {
        var slug =
            request.Slug
                .Trim()
                .ToLowerInvariant();

        var recipe =
            await recipeRepository.GetBySlugWithDetailsAsync(
                slug,
                ct);

        if (recipe is null)
        {
            throw new RecipeNotFoundException(
                request.Slug);
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
        if (status == RecipeStatus.Published)
        {
            return;
        }

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