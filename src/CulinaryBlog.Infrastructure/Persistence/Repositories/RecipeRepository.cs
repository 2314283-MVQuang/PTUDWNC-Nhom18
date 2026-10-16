using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cài đặt truy vấn đặc thù cho Recipe.
/// CRUD cơ bản dùng lại RepositoryBase.
/// </summary>
public class RecipeRepository(CulinaryBlogDbContext dbContext)
    : RepositoryBase<Recipe>(dbContext), IRecipeRepository
{
    public Task<Recipe?> GetByIdForPublishingAsync(
        Guid id,
        CancellationToken ct = default) =>
        DbSet
            .Include(x => x.Steps)
            .Include(x => x.Ingredients)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Recipe?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken ct = default) =>
        DbSet
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Author)
            .Include(x => x.Steps.OrderBy(x => x.StepNumber))
            .Include(x => x.Ingredients.OrderBy(x => x.OrderIndex))
            .Include(x => x.Images.OrderBy(x => x.OrderIndex))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Recipe?> GetBySlugWithDetailsAsync(
        string slug,
        CancellationToken ct = default) =>
        DbSet
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Author)
            .Include(x => x.Steps.OrderBy(x => x.StepNumber))
            .Include(x => x.Ingredients.OrderBy(x => x.OrderIndex))
            .Include(x => x.Images.OrderBy(x => x.OrderIndex))
            .FirstOrDefaultAsync(x => x.Slug == slug, ct);

    public Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludeRecipeId = null,
        CancellationToken ct = default)
    {
        var query = DbSet
            .IgnoreQueryFilters()
            .Where(x => x.Slug == slug);

        if (excludeRecipeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeRecipeId.Value);
        }

        return query.AnyAsync(ct);
    }

    public void SetOriginalRowVersion(
        Recipe recipe,
        byte[] expectedRowVersion)
    {
        DbContext
            .Entry(recipe)
            .Property(x => x.RowVersion)
            .OriginalValue = expectedRowVersion;
    }

    public async Task<IReadOnlyList<Recipe>> SearchRecipesAsync(
        string keyword,
        CancellationToken ct = default)
    {
        var (items, _) = await SearchRecipesPagedAsync(keyword, 1, 50, ct: ct);
        return items;
    }

    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> SearchRecipesPagedAsync(
        string keyword,
        int page,
        int pageSize,
        Guid? categoryId = null,
        RecipeDifficulty? difficulty = null,
        int? maxCookTime = null,
        int? minServings = null,
        string? sort = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return ([], 0);
        }

        var normalizedKeyword = keyword.Trim();
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 12 : (pageSize > 50 ? 50 : pageSize);
        var offset = (page - 1) * pageSize;

        // Fallback cho môi trường test (InMemory DbContext không hỗ trợ SqlQuery)
        if (!DbContext.Database.IsRelational())
        {
            var inMemQuery = DbSet
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Author)
                .Include(x => x.Images)
                .Where(x => x.Status == RecipeStatus.Published);

            inMemQuery = inMemQuery.Where(x =>
                x.Title.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase) ||
                (x.Description != null && x.Description.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)));

            if (categoryId.HasValue)
            {
                inMemQuery = inMemQuery.Where(x => x.CategoryId == categoryId.Value);
            }

            if (difficulty.HasValue)
            {
                inMemQuery = inMemQuery.Where(x => x.Difficulty == difficulty.Value);
            }

            if (maxCookTime.HasValue)
            {
                inMemQuery = inMemQuery.Where(x => x.CookTime <= maxCookTime.Value);
            }

            if (minServings.HasValue)
            {
                inMemQuery = inMemQuery.Where(x => x.Servings >= minServings.Value);
            }

            var inMemTotal = await inMemQuery.CountAsync(ct);

            inMemQuery = sort?.Trim().ToLowerInvariant() switch
            {
                "title" => inMemQuery.OrderBy(x => x.Title).ThenByDescending(x => x.CreatedAt),
                "-title" => inMemQuery.OrderByDescending(x => x.Title).ThenByDescending(x => x.CreatedAt),
                "cooktime" => inMemQuery.OrderBy(x => x.CookTime).ThenByDescending(x => x.CreatedAt),
                "-cooktime" => inMemQuery.OrderByDescending(x => x.CookTime).ThenByDescending(x => x.CreatedAt),
                "createdat" => inMemQuery.OrderBy(x => x.CreatedAt),
                _ => inMemQuery.OrderByDescending(x => x.CreatedAt)
            };

            var inMemItems = await inMemQuery.Skip(offset).Take(pageSize).ToListAsync(ct);
            return (inMemItems, inMemTotal);
        }

        // Môi trường PostgreSQL: Full-Text Search qua GIN index và tsvector
        int? difficultyInt = difficulty.HasValue ? (int)difficulty.Value : null;

        var totalCount = (await DbContext.Database
            .SqlQuery<int>($"""
                SELECT CAST(COUNT(*) AS integer) AS "Value"
                FROM "Recipes" AS r
                WHERE r."IsDeleted" = FALSE
                  AND r."Status" = 1
                  AND ({categoryId}::uuid IS NULL OR r."CategoryId" = {categoryId})
                  AND ({difficultyInt}::integer IS NULL OR r."Difficulty" = {difficultyInt})
                  AND ({maxCookTime}::integer IS NULL OR r."CookTime" <= {maxCookTime})
                  AND ({minServings}::integer IS NULL OR r."Servings" >= {minServings})
                  AND r."SearchVector" @@ websearch_to_tsquery('vietnamese', unaccent({normalizedKeyword}))
                """)
            .ToListAsync(ct)).FirstOrDefault();

        if (totalCount == 0)
        {
            return ([], 0);
        }

        var rankedIds = await DbContext.Database
            .SqlQuery<Guid>($"""
                SELECT r."Id" AS "Value"
                FROM "Recipes" AS r
                WHERE r."IsDeleted" = FALSE
                  AND r."Status" = 1
                  AND ({categoryId}::uuid IS NULL OR r."CategoryId" = {categoryId})
                  AND ({difficultyInt}::integer IS NULL OR r."Difficulty" = {difficultyInt})
                  AND ({maxCookTime}::integer IS NULL OR r."CookTime" <= {maxCookTime})
                  AND ({minServings}::integer IS NULL OR r."Servings" >= {minServings})
                  AND r."SearchVector" @@ websearch_to_tsquery('vietnamese', unaccent({normalizedKeyword}))
                ORDER BY ts_rank(r."SearchVector", websearch_to_tsquery('vietnamese', unaccent({normalizedKeyword}))) DESC
                OFFSET {offset}
                LIMIT {pageSize}
                """)
            .ToListAsync(ct);

        if (rankedIds.Count == 0)
        {
            return ([], totalCount);
        }

        var recipes = await DbSet
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Author)
            .Include(x => x.Images)
            .Where(x => rankedIds.Contains(x.Id) && x.Status == RecipeStatus.Published)
            .ToListAsync(ct);

        var order = rankedIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        var orderedRecipes = recipes
            .OrderBy(x => order[x.Id])
            .ToList();

        return (orderedRecipes, totalCount);
    }
}
