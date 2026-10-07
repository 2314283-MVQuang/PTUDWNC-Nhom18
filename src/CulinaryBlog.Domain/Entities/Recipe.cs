using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// Công thức nấu ăn — aggregate root của hệ thống (bảng "Recipes" — mục 7.2).
/// Chứa các child entity: Steps, Ingredients, Images và Owned Entity Nutrition.
/// Mọi thay đổi phải đi qua IUnitOfWork để đảm bảo tính transaction (mục 4.3).
/// </summary>
public class Recipe : BaseEntity
{
    public string Title { get; set; } = null!;

    /// <summary>Sinh từ Title, KHÔNG đổi sau khi Publish (SEO — mục 5.7).</summary>
    public string Slug { get; set; } = null!;

    /// <summary>Mô tả ngắn, ≤2000 ký tự (validate ở FluentValidation, không validate ở đây).</summary>
    public string Description { get; set; } = null!;

    /// <summary>Hướng dẫn tổng quan dạng markdown — trường "legacy", chi tiết từng bước dùng Steps.</summary>
    public string Instructions { get; set; } = null!;

    /// <summary>Phút.</summary>
    public int PrepTime { get; set; }

    /// <summary>Phút, 0 cho "No cook".</summary>
    public int CookTime { get; set; }

    public int Servings { get; set; }

    public RecipeDifficulty Difficulty { get; set; } = RecipeDifficulty.Easy;

    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public string AuthorId { get; set; } = null!;

    public ApplicationUser Author { get; set; } = null!;

    /// <summary>
    /// CỘT NÀY KHÔNG ĐƯỢC MAP VÀO C# (xem RecipeConfiguration.cs): cột "SearchVector" (tsvector)
    /// trong PostgreSQL được trigger "trg_Recipes_search_vector" tự cập nhật từ Title +
    /// Description mỗi khi INSERT/UPDATE (xem db/init/02-schema.sql) — EF Core không cần và
    /// không nên đụng vào.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    public RecipeNutrition Nutrition { get; set; } = new();

    public ICollection<RecipeStep> Steps { get; set; } = [];

    public ICollection<RecipeIngredient> Ingredients { get; set; } = [];

    public ICollection<RecipeImage> Images { get; set; } = [];

    /// <Sumary>
    /// Cập nhật các thông tin cơ bản của Recipe
    /// Field nào truyền null thì giữ nguyên giá trị cũ
    /// Điều này phù hợp với PUT body hiện tại của frontend
    /// vì một số field có thể không được gửi
    /// </Sumary>
    public void UpdateBasicInfo(string? title, string? description, string? instructions, int? prepTime, int? cookTime, int? servings, RecipeDifficulty? difficulty, Guid? categoryId)
    {
        if (title != null) Title = title;
        if (description != null) Description = description;
        if (instructions != null) Instructions = instructions;
        if (prepTime.HasValue) PrepTime = prepTime.Value;
        if (cookTime.HasValue) CookTime = cookTime.Value;
        if (servings.HasValue) Servings = servings.Value;
        if (difficulty.HasValue) Difficulty = difficulty.Value;
        if (categoryId.HasValue) CategoryId = categoryId.Value;
    }

    // <summary>
    // Cập nhật slug
    // Business rule: Slug chỉ được cập nhật khi Recipe chưa được publish
    // được quyết định ở Application layer (RecipeService) trước khi gọi hàm này
    // </summary>
    public void ChangeSlug(string slug)
    {
        Slug = slug;
    }

    /// <summary>
    /// FR-RCP-006: lưu trữ công thức — ẩn khỏi mọi danh sách/tìm kiếm công khai nhưng KHÔNG xoá dữ
    /// liệu (khác xoá mềm <c>IsDeleted</c> của FR-RCP-007). Gọi lại trên công thức đã lưu trữ thì
    /// không đổi gì (idempotent). <c>PublishedAt</c> được giữ nguyên để <see cref="Unarchive"/> biết
    /// trước khi lưu trữ công thức đã từng xuất bản hay chưa.
    /// </summary>
    public void Archive()
    {
        Status = RecipeStatus.Archived;
    }

    /// <summary>
    /// FR-RCP-006: bỏ lưu trữ — trả công thức về trạng thái trước khi lưu trữ: đã từng xuất bản
    /// (có <c>PublishedAt</c>) thì về <c>Published</c>, chưa thì về <c>Draft</c>. Công thức không ở
    /// trạng thái <c>Archived</c> thì không đổi gì (idempotent).
    /// </summary>
    public void Unarchive()
    {
        if (Status != RecipeStatus.Archived)
        {
            return;
        }

        Status = PublishedAt.HasValue
            ? RecipeStatus.Published
            : RecipeStatus.Draft;
    }

    // <summary>
    // Cập nhật toàn bộ Nutrition nếu request gửi Nutrition
    // </summary>
    public void UpdateNutrition(RecipeNutrition nutrition)
    {
        Nutrition.Calories = nutrition.Calories;
        Nutrition.Protein = nutrition.Protein;
        Nutrition.Carbohydrates = nutrition.Carbohydrates;
        Nutrition.Fat = nutrition.Fat;
        Nutrition.Fiber = nutrition.Fiber;
        Nutrition.Sodium = nutrition.Sodium;
    }
}
