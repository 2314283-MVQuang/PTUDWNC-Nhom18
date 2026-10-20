namespace CulinaryBlog.Domain.Interfaces;

/// <summary>FR-SRCH-003: các field được phép sắp xếp kết quả tìm kiếm (whitelist).</summary>
public enum RecipeSearchSortField
{
    CreatedAt,
    PublishedAt,
    Title,
    PrepTime,
    CookTime,
}

/// <summary>FR-SRCH-003: field + chiều sắp xếp. null ở repository nghĩa là theo độ liên quan.</summary>
public sealed record RecipeSearchSort(RecipeSearchSortField Field, bool IsDescending);
