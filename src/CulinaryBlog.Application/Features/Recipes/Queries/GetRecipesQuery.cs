using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Enums;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipes;

/// <summary>
/// FR-RCP-001.
/// GET /api/v1/recipes
/// Mine = true (trang quản lý "Công thức của tôi"): chỉ trả công thức do chính người đang đăng
/// nhập đăng — gồm cả Draft/Archived — áp dụng cho cả Author lẫn Admin.
/// Status: lọc thêm theo trạng thái (trang Admin "Tất cả công thức"); chỉ lọc TRONG phạm vi
/// người gọi được xem, không mở rộng quyền (khách lọc Draft sẽ nhận danh sách rỗng).
/// </summary>
public sealed record GetRecipesQuery(
    int Page = 1,
    int PageSize = 12,
    Guid? CategoryId = null,
    RecipeDifficulty? Difficulty = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string? Sort = "-createdAt",
    bool Mine = false,
    RecipeStatus? Status = null)
    : IRequest<PagedResult<RecipeSummaryDto>>;