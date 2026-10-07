using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using RecipeRepository = CulinaryBlog.Domain.Interfaces.IRecipeRepository;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipes;

public sealed class GetRecipesQueryHandler(
    RecipeRepository recipeRepository,
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
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Author)
            .Include(x => x.Images);

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

        query = ApplySort(
            query,
            request.Sort);

        var totalCount =
            await query.CountAsync(ct);

        var entities =
            await query
                .Skip(
                    (request.Page - 1) *
                    request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

        var items =
            entities
                .Select(x => x.ToSummaryDto())
                .ToList();

        return PagedResult<RecipeSummaryDto>.Create(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    private static IQueryable<Recipe> ApplySort(
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