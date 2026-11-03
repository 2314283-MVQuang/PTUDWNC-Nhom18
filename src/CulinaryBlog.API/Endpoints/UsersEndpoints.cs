using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Users.Commands.SetUserStatus;
using CulinaryBlog.Application.Features.Users.Dtos;
using CulinaryBlog.Application.Features.Users.Queries.GetUsers;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

public sealed record SetUserStatusRequest(bool IsActive);

/// <summary>
/// Tuần 9 (Quang) — quản lý người dùng cho Admin (trang /dashboard/users).
/// Cả nhóm endpoint chỉ Admin; không cache (dữ liệu nhạy cảm, đổi trạng thái phải thấy ngay).
/// </summary>
public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapV1Group("/users")
            .WithTags("Users")
            .RequireAuthorization(AuthorizationPolicies.Admin);

        // GET /api/v1/users?q=&role=Admin|Author&isActive=true|false&page=1&pageSize=20
        group.MapGet("", async (
            string? q, string? role, bool? isActive, int? page, int? pageSize,
            ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUsersQuery(
                q, string.IsNullOrWhiteSpace(role) ? null : role.Trim(), isActive,
                page ?? 1, pageSize ?? GetUsersQuery.DefaultPageSize), ct);
            return result.ToPagedResponse();
        })
        .WithName("GetUsers")
        .WithSummary("Danh sách người dùng (Admin, tác giả) có tìm kiếm, lọc và phân trang — chỉ Admin")
        .Produces<PagedApiEnvelope<UserSummaryDto>>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        // PATCH /api/v1/users/{id}/status { "isActive": false }
        group.MapPatch("/{id}/status", async (
            string id, SetUserStatusRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetUserStatusCommand(id, request.IsActive), ct);
            return result.ToOkResponse();
        })
        .WithName("SetUserStatus")
        .WithSummary("Khoá / mở khoá tài khoản — chỉ Admin, không tự khoá chính mình")
        .Produces<ApiEnvelope<UserSummaryDto>>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
