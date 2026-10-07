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
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        var normalizedKeyword = keyword.Trim();

        // Cột "SearchVector" (tsvector) do trigger PostgreSQL quản lý và KHÔNG map vào model EF
        // (xem ghi chú đầu CulinaryBlogDbContext.cs), nên truy vấn bằng SQL tham số hoá: bước 1 lấy
        // danh sách Id đã xếp hạng theo ts_rank (dùng GIN index), bước 2 nạp entity bằng EF.
        // Cấu hình text search "vietnamese" + extension unaccent được tạo cùng schema database.
        var rankedIds = await DbContext.Database
            .SqlQuery<Guid>($"""
                SELECT r."Id" AS "Value"
                FROM "Recipes" AS r
                WHERE r."IsDeleted" = FALSE
                  AND r."SearchVector" @@ websearch_to_tsquery('vietnamese', unaccent({normalizedKeyword}))
                ORDER BY ts_rank(r."SearchVector", websearch_to_tsquery('vietnamese', unaccent({normalizedKeyword}))) DESC
                LIMIT 50
                """)
            .ToListAsync(ct);

        if (rankedIds.Count == 0)
        {
            return [];
        }

        var recipes = await DbSet
            .AsNoTracking()
            .Where(x => rankedIds.Contains(x.Id) && x.Status == RecipeStatus.Published)
            .ToListAsync(ct);

        var order = rankedIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        return recipes
            .OrderBy(x => order[x.Id])
            .ToList();
    }
}
