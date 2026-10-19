using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Domain.Interfaces;

/// <summary>FR-SRCH-002: các điều kiện giao nhau; null nghĩa là không lọc.</summary>
public sealed record RecipeSearchFilters(
    Guid? CategoryId = null,
    RecipeDifficulty? Difficulty = null,
    int? MinPrepTime = null,
    int? MaxPrepTime = null,
    int? MinCookTime = null,
    int? MaxCookTime = null);
