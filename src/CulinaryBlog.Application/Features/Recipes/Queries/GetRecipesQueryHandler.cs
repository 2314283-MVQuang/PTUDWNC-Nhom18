using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipes;

public sealed class GetRecipesQueryHandler(
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser)
    : IRequestHandler<
        GetRecipesQuery,
        PagedResult<RecipeSummaryDto>>
{
    public async Task<PagedResult<RecipeSummaryDto>> Handle(
        GetRecipesQuery request,
        CancellationToken ct)
    {
        IQueryable<Recipe> query = recipeRepository
            .Query()
            .AsNoTracking();

        // Guest:
        // chỉ Published.
        //
        // User đăng nhập:
        // Published + Recipe của chính mình.
        //
        // Admin:
        // tất cả Recipe không bị soft-delete.
        query = query.Where(x =>
            x.Status == RecipeStatus.Published ||
            currentUser.IsInRole("Admin") ||
            (!string.IsNullOrWhiteSpace(currentUser.UserId) &&
             x.AuthorId == currentUser.UserId));

        if (request.CategoryId.HasValue)
        {
            query = query.Where(
                x => x.CategoryId == request.CategoryId.Value);
        }

        if (request.Difficulty.HasValue)
        {
            query = query.Where(
                x => x.Difficulty == request.Difficulty.Value);
        }

        if (request.MaxCookTime.HasValue)
        {
            query = query.Where(
                x => x.CookTime <= request.MaxCookTime.Value);
        }

        if (request.MinServings.HasValue)
        {
            query = query.Where(
                x => x.Servings >= request.MinServings.Value);
        }

        var ordered = ApplySort(
            query,
            request.Sort);

        var totalCount =
            await query.CountAsync(ct);

        // ThenBy(Id): 2 công thức trùng CreatedAt/Title không bị lặp hoặc mất giữa 2 trang.
        var items =
            await ordered.ThenBy(x => x.Id)
                .Skip(
                    (request.Page - 1) *
                    request.PageSize)
                .Take(request.PageSize)
                .Select(RecipeDtoMapping.SummaryProjection)
                .ToListAsync(ct);

        return PagedResult<RecipeSummaryDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    private static IOrderedQueryable<Recipe> ApplySort(
        IQueryable<Recipe> query,
        string? sort)
    {
        return sort?
            .Trim()
            .ToLowerInvariant() switch
        {
            "title" =>
                query
                    .OrderBy(x => x.Title)
                    .ThenByDescending(x => x.CreatedAt),

            "-title" =>
                query
                    .OrderByDescending(x => x.Title)
                    .ThenByDescending(x => x.CreatedAt),

            "cooktime" =>
                query
                    .OrderBy(x => x.CookTime)
                    .ThenByDescending(x => x.CreatedAt),

            "-cooktime" =>
                query
                    .OrderByDescending(x => x.CookTime)
                    .ThenByDescending(x => x.CreatedAt),

            "createdat" =>
                query.OrderBy(x => x.CreatedAt),

            _ =>
                query.OrderByDescending(
                    x => x.CreatedAt)
        };
    }
}
