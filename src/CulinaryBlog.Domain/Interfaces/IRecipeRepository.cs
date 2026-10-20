using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Domain.Interfaces;

/// <summary>
/// Repository chuyên biệt cho Recipe Aggregate Root.
/// Kế thừa IRepository để dùng các thao tác CRUD/query cơ bản,
/// đồng thời bổ sung truy vấn đặc thù của Recipe.
/// </summary>
public interface IRecipeRepository : IRepository<Recipe>
{
    /// <summary>
    /// Lấy aggregate có tracking để xuất bản, kèm các bước và nguyên liệu còn hiệu lực.
    /// Khác truy vấn detail chỉ đọc, thay đổi trạng thái ở đây được UnitOfWork lưu lại.
    /// </summary>
    Task<Recipe?> GetByIdForPublishingAsync(
        Guid id,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy Recipe đầy đủ cho màn hình chi tiết:
    /// Category, Author, Steps, Ingredients, Images.
    /// </summary>
    Task<Recipe?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy Recipe theo slug để phục vụ trang public:
    /// /recipes/{slug}
    /// </summary>
    Task<Recipe?> GetBySlugWithDetailsAsync(
        string slug,
        CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra slug đã được sử dụng chưa.
    /// IgnoreQueryFilters được dùng để sau này không tái sử dụng nhầm slug
    /// của một recipe đã soft-delete.
    /// </summary>
    Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludeRecipeId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Đặt RowVersion mà client đã đọc thành OriginalValue của EF Core.
    /// Nhờ đó UPDATE sẽ có điều kiện concurrency theo RowVersion cũ.
    /// </summary>
    void SetOriginalRowVersion(
        Recipe recipe,
        byte[] expectedRowVersion);

    /// <summary>
    /// FR-SRCH-001: tìm công thức ĐÃ XUẤT BẢN bằng full-text search tiếng Việt (bỏ dấu).
    /// FR-SRCH-002: lọc kết hợp AND theo <paramref name="filters"/>.
    /// FR-SRCH-003: sắp xếp theo <paramref name="sort"/>; null = độ liên quan giảm dần (ts_rank).
    /// FR-SRCH-004: phân trang offset — trả về trang <paramref name="page"/> và tổng số kết quả
    /// sau lọc. Lọc, đếm, sắp xếp và OFFSET/LIMIT đều chạy trong SQL. Từ khóa rỗng trả ([], 0).
    /// </summary>
    Task<(IReadOnlyList<Recipe> Items, int TotalCount)> SearchRecipesAsync(
        string keyword,
        RecipeSearchFilters? filters,
        RecipeSearchSort? sort,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
