using System.Runtime.CompilerServices;
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
        CancellationToken ct = default,
        RecipeSearchFilters? filters = null,
        RecipeSearchSort? sort = null)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        var normalizedKeyword = keyword.Trim();
        filters ??= new RecipeSearchFilters();
        var difficulty = (int?)filters.Difficulty;

        // Cột "SearchVector" (tsvector) do trigger PostgreSQL quản lý và KHÔNG map vào model EF
        // (xem ghi chú đầu CulinaryBlogDbContext.cs), nên truy vấn bằng SQL tham số hoá: bước 1 lấy
        // danh sách Id đã lọc + sắp xếp (dùng GIN index), bước 2 nạp entity bằng EF.
        // Cấu hình text search "vietnamese" + extension unaccent được tạo cùng schema database.
        //
        // Mọi giá trị người dùng nhập đi qua tham số {0}..{n} (EF tự parameterize). Riêng mệnh đề
        // ORDER BY không tham số hoá được nên lấy từ whitelist cố định ở SearchOrderBy() — không bao
        // giờ ghép chuỗi người dùng vào SQL.
        var sql = $$"""
            SELECT r."Id" AS "Value"
            FROM "Recipes" AS r
            WHERE r."IsDeleted" = FALSE
              AND r."Status" = {1}
              AND (CAST({2} AS uuid) IS NULL OR r."CategoryId" = CAST({2} AS uuid))
              AND (CAST({3} AS integer) IS NULL OR r."Difficulty" = CAST({3} AS integer))
              AND (CAST({4} AS integer) IS NULL OR r."PrepTime" >= CAST({4} AS integer))
              AND (CAST({5} AS integer) IS NULL OR r."PrepTime" <= CAST({5} AS integer))
              AND (CAST({6} AS integer) IS NULL OR r."CookTime" >= CAST({6} AS integer))
              AND (CAST({7} AS integer) IS NULL OR r."CookTime" <= CAST({7} AS integer))
              AND (CAST({8} AS integer) IS NULL OR r."Servings" >= CAST({8} AS integer))
              AND r."SearchVector" @@ websearch_to_tsquery('vietnamese', unaccent({0}))
            ORDER BY {{SearchOrderBy(sort)}}
            LIMIT 50
            """;

        var rankedIds = await DbContext.Database
            .SqlQuery<Guid>(FormattableStringFactory.Create(
                sql,
                normalizedKeyword,
                (int)RecipeStatus.Published,
                filters.CategoryId,
                difficulty,
                filters.MinPrepTime,
                filters.MaxPrepTime,
                filters.MinCookTime,
                filters.MaxCookTime,
                filters.MinServings))
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

    /// <summary>
    /// FR-SRCH-003: ORDER BY lấy từ whitelist cố định. Luôn thêm r."Id" cuối cùng để thứ tự ổn định
    /// khi nhiều công thức trùng giá trị (vd. cùng cookTime). {0} là từ khóa đã tham số hoá.
    /// </summary>
    private static string SearchOrderBy(RecipeSearchSort? sort)
    {
        if (sort is null)
        {
            return """ts_rank(r."SearchVector", websearch_to_tsquery('vietnamese', unaccent({0}))) DESC, r."CreatedAt" DESC, r."Id" """;
        }

        var column = sort.Field switch
        {
            RecipeSearchSortField.CreatedAt => "r.\"CreatedAt\"",
            RecipeSearchSortField.PublishedAt => "r.\"PublishedAt\"",
            RecipeSearchSortField.Title => "r.\"Title\"",
            RecipeSearchSortField.PrepTime => "r.\"PrepTime\"",
            RecipeSearchSortField.CookTime => "r.\"CookTime\"",
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Field, "Field sắp xếp không hỗ trợ."),
        };

        return $"{column} {(sort.IsDescending ? "DESC NULLS LAST" : "ASC NULLS LAST")}, r.\"Id\"";
    }
}
