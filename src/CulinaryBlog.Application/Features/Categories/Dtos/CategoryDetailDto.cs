using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Recipes.Dtos;

namespace CulinaryBlog.Application.Features.Categories.Dtos;

/// <summary>
/// FR-CAT-002 — kết quả GET /api/v1/categories/{slug}: thông tin danh mục kèm danh sách công thức
/// thuộc danh mục đó, có phân trang. Khi trả về JSON có dạng
/// { category: {...}, recipes: { items, page, pageSize, totalCount, totalPages, ... } } đúng SRS.
/// </summary>
public sealed record CategoryDetailDto(
    CategoryDto Category,
    PagedResult<RecipeSummaryDto> Recipes);
