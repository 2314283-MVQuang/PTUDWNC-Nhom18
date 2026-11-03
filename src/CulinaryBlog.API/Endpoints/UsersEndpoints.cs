using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Users;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1Group("/users").WithTags("Users").RequireAuthorization(AuthorizationPolicies.Admin)
            .MapGet("", async (HttpContext http, ISender sender, CancellationToken ct, int page = 1, int pageSize = 12, string? role = null, string? q = null) =>
            {
                http.Response.Headers.CacheControl = "no-store";
                return (await sender.Send(new GetUsersQuery(page, pageSize, role, q), ct)).ToPagedResponse();
            })
            .WithName("GetUsers").WithSummary("Danh sách người dùng dành cho quản trị viên")
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(422);
    }
}
