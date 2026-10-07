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
    /// FR-SRCH-001: tìm công thức ĐÃ XUẤT BẢN bằng full-text search tiếng Việt (bỏ dấu), sắp theo
    /// độ liên quan giảm dần. Từ khóa rỗng trả về danh sách rỗng.
    /// </summary>
    Task<IReadOnlyList<Recipe>> SearchRecipesAsync(
        string keyword,
        CancellationToken ct = default);
}