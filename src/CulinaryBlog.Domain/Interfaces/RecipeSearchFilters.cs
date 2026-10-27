using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Domain.Interfaces;

/// <summary>
/// FR-SRCH-002: các điều kiện giao nhau (AND); null nghĩa là không lọc. SRS liệt kê categoryId,
/// difficulty, maxCookTime, minServings — nhóm bổ sung thêm khoảng prepTime/cookTime hai đầu.
/// </summary>
public sealed record RecipeSearchFilters(
    Guid? CategoryId = null,
    RecipeDifficulty? Difficulty = null,
    int? MinPrepTime = null,
    int? MaxPrepTime = null,
    int? MinCookTime = null,
    int? MaxCookTime = null,
    int? MinServings = null);
