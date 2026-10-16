using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Enums;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

/// <summary>
/// FR-SRCH-001 & FR-SRCH-004 (Tiến - Tuần 6):
/// Truy vấn tìm kiếm công thức toàn văn với phân trang (page, pageSize) và bộ lọc kết hợp.
/// </summary>
public sealed record SearchRecipesQuery(
    string Keyword = "",
    int Page = 1,
    int PageSize = 12,
    Guid? CategoryId = null,
    RecipeDifficulty? Difficulty = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string? Sort = null)
    : IRequest<PagedResult<RecipeSummaryDto>>;