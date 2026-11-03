using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Enums;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

/// <summary>
/// FR-SRCH-001 tìm toàn văn, FR-SRCH-002 bộ lọc (Ý), FR-SRCH-003 sắp xếp (Thịnh),
/// FR-SRCH-004 phân trang offset (Tiến): <c>page</c> mặc định 1, <c>pageSize</c> mặc định 12, tối đa 50.
/// <c>Sort</c>: tên field, thêm tiền tố "-" để giảm dần; null/rỗng = theo độ liên quan (ts_rank).
/// </summary>
public record SearchRecipesQuery(
    string Keyword = "",
    Guid? CategoryId = null,
    RecipeDifficulty? Difficulty = null,
    int? MinPrepTime = null,
    int? MaxPrepTime = null,
    int? MinCookTime = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = SearchRecipesQuery.DefaultPageSize) : IRequest<PagedResult<RecipeSummaryDto>>
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 50;
}
