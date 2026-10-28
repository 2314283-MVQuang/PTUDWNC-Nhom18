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
            // Tách 3 collection để tránh nhân số dòng Steps × Ingredients × Images.
            .AsSplitQuery()
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
            .AsSplitQuery()
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

    public async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> SearchRecipesAsync(
        string keyword,
        RecipeSearchFilters? filters,
        RecipeSearchSort? sort,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return ([], 0);
        }

        var normalizedKeyword = keyword.Trim();
        filters ??= new RecipeSearchFilters();
        var offset = (Math.Max(page, 1) - 1) * pageSize;

        // InMemory (unit test) không chạy được SQL full-text của PostgreSQL — dùng bản LINQ tương
        // đương (tìm chuỗi con trong tiêu đề/mô tả) để kiểm thử lọc, sắp xếp và phân trang.
        if (!DbContext.Database.IsRelational())
        {
            return await SearchInMemoryAsync(normalizedKeyword, filters, sort, offset, pageSize, ct);
        }

        // Cột "SearchVector" (tsvector) do trigger PostgreSQL quản lý và KHÔNG map vào model EF
        // (xem ghi chú đầu CulinaryBlogDbContext.cs), nên truy vấn bằng SQL tham số hoá: bước 1 đếm
        // tổng và lấy Id của đúng trang cần (dùng GIN index), bước 2 nạp entity bằng EF.
        // Cấu hình text search "vietnamese" + extension unaccent được tạo cùng schema database.
        //
        // Mọi giá trị người dùng nhập đi qua tham số {0}..{n} (EF tự parameterize). Riêng mệnh đề
        // ORDER BY không tham số hoá được nên lấy từ whitelist cố định ở SearchOrderBy() — không bao
        // giờ ghép chuỗi người dùng vào SQL.
        const string where = """
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
            """;
        object?[] args =
        [
            normalizedKeyword,
            (int)RecipeStatus.Published,
            filters.CategoryId,
            (int?)filters.Difficulty,
            filters.MinPrepTime,
            filters.MaxPrepTime,
            filters.MinCookTime,
            filters.MaxCookTime,
            filters.MinServings,
            offset,
            pageSize,
        ];

        var totalCount = (await DbContext.Database
            .SqlQuery<int>(FormattableStringFactory.Create(
                $"""SELECT CAST(COUNT(*) AS integer) AS "Value" {where}""", args[..9]))
            .ToListAsync(ct)).SingleOrDefault();

        if (totalCount == 0 || offset >= totalCount)
        {
            return ([], totalCount);
        }

        var pageIds = await DbContext.Database
            .SqlQuery<Guid>(FormattableStringFactory.Create(
                $$"""
                SELECT r."Id" AS "Value" {{where}}
                ORDER BY {{SearchOrderBy(sort)}}
                OFFSET {9} LIMIT {10}
                """,
                args))
            .ToListAsync(ct);

        // Category/Author/Images để map RecipeSummaryDto (tên danh mục, tác giả, ảnh đại diện).
        var recipes = await DbSet
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Author)
            .Include(x => x.Images)
            .Where(x => pageIds.Contains(x.Id) && x.Status == RecipeStatus.Published)
            .ToListAsync(ct);

        var order = pageIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        return (recipes.OrderBy(x => order[x.Id]).ToList(), totalCount);
    }

    private async Task<(IReadOnlyList<Recipe> Items, int TotalCount)> SearchInMemoryAsync(
        string keyword,
        RecipeSearchFilters filters,
        RecipeSearchSort? sort,
        int offset,
        int pageSize,
        CancellationToken ct)
    {
        var query = DbSet
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Author)
            .Include(x => x.Images)
            .Where(x => x.Status == RecipeStatus.Published)
            .Where(x =>
                x.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (x.Description != null && x.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        if (filters.CategoryId is { } categoryId) query = query.Where(x => x.CategoryId == categoryId);
        if (filters.Difficulty is { } difficulty) query = query.Where(x => x.Difficulty == difficulty);
        if (filters.MinPrepTime is { } minPrep) query = query.Where(x => x.PrepTime >= minPrep);
        if (filters.MaxPrepTime is { } maxPrep) query = query.Where(x => x.PrepTime <= maxPrep);
        if (filters.MinCookTime is { } minCook) query = query.Where(x => x.CookTime >= minCook);
        if (filters.MaxCookTime is { } maxCook) query = query.Where(x => x.CookTime <= maxCook);
        if (filters.MinServings is { } minServings) query = query.Where(x => x.Servings >= minServings);

        var totalCount = await query.CountAsync(ct);

        // Không có ts_rank trong InMemory: "độ liên quan" tạm thay bằng mới nhất trước.
        var ordered = sort switch
        {
            null => query.OrderByDescending(x => x.CreatedAt),
            { Field: RecipeSearchSortField.CreatedAt, IsDescending: false } => query.OrderBy(x => x.CreatedAt),
            { Field: RecipeSearchSortField.CreatedAt } => query.OrderByDescending(x => x.CreatedAt),
            { Field: RecipeSearchSortField.PublishedAt, IsDescending: false } => query.OrderBy(x => x.PublishedAt),
            { Field: RecipeSearchSortField.PublishedAt } => query.OrderByDescending(x => x.PublishedAt),
            { Field: RecipeSearchSortField.Title, IsDescending: false } => query.OrderBy(x => x.Title),
            { Field: RecipeSearchSortField.Title } => query.OrderByDescending(x => x.Title),
            { Field: RecipeSearchSortField.PrepTime, IsDescending: false } => query.OrderBy(x => x.PrepTime),
            { Field: RecipeSearchSortField.PrepTime } => query.OrderByDescending(x => x.PrepTime),
            { Field: RecipeSearchSortField.CookTime, IsDescending: false } => query.OrderBy(x => x.CookTime),
            { Field: RecipeSearchSortField.CookTime } => query.OrderByDescending(x => x.CookTime),
            _ => throw new ArgumentOutOfRangeException(nameof(sort), "Field sắp xếp không hỗ trợ."),
        };

        var items = await ordered.ThenBy(x => x.Id).Skip(offset).Take(pageSize).ToListAsync(ct);
        return (items, totalCount);
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
