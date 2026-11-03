using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Users.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Users.Queries.GetUsers;

/// <summary>
/// Tuần 9 (Quang) — GET /api/v1/users (chỉ Admin): danh sách người dùng có phân trang.
/// <paramref name="Role"/>: "Admin" = quản trị viên, "Author" = tác giả thường (không phải Admin), null = tất cả.
/// <paramref name="IsActive"/>: lọc theo trạng thái hoạt động/bị khoá, null = tất cả.
/// </summary>
public sealed record GetUsersQuery(
    string? Keyword = null,
    string? Role = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = GetUsersQuery.DefaultPageSize) : IRequest<PagedResult<UserSummaryDto>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const string AdminRole = "Admin";
    public const string AuthorRole = "Author";
}
