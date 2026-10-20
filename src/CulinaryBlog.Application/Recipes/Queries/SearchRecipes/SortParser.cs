using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Interfaces;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

/// <summary>
/// FR-SRCH-003: đọc tham số <c>sort</c> dạng "field" (tăng dần) hoặc "-field" (giảm dần).
/// Chỉ nhận field trong whitelist — tên cột KHÔNG BAO GIỜ lấy thẳng từ input người dùng để ghép
/// vào SQL; repository chỉ nhận enum <see cref="RecipeSearchSortField"/>.
/// </summary>
public static class SortParser
{
    private static readonly IReadOnlyDictionary<string, RecipeSearchSortField> Fields =
        new Dictionary<string, RecipeSearchSortField>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdAt"] = RecipeSearchSortField.CreatedAt,
            ["publishedAt"] = RecipeSearchSortField.PublishedAt,
            ["title"] = RecipeSearchSortField.Title,
            ["prepTime"] = RecipeSearchSortField.PrepTime,
            ["cookTime"] = RecipeSearchSortField.CookTime,
        };

    /// <summary>Trả null khi không truyền sort (giữ thứ tự theo độ liên quan ts_rank).</summary>
    public static RecipeSearchSort? Parse(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return null;
        }

        var value = sort.Trim();
        var isDescending = value.StartsWith('-');
        var field = isDescending ? value[1..] : value;

        if (!Fields.TryGetValue(field, out var parsed))
        {
            throw new InvalidSortException(field, Fields.Keys);
        }

        return new RecipeSearchSort(parsed, isDescending);
    }
}
