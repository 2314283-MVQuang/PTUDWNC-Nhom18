using System.Security.Claims;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Auth.Commands.Login;
using CulinaryBlog.Application.Features.Auth.Commands.Logout;
using CulinaryBlog.Application.Features.Auth.Commands.Refresh;
using CulinaryBlog.Application.Features.Auth.Commands.Register;
using CulinaryBlog.Application.Features.Auth.Commands.UpdateProfile;
using CulinaryBlog.Application.Features.Auth.Queries.Me;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // ---------------------------------------------------------------------------
        // FR-AUTH-001 & FR-AUTH-002: Hỗ trợ cả /auth/... và /api/v1/auth/...
        // ---------------------------------------------------------------------------
        var groups = new[]
        {
            app.MapGroup("/auth").WithTags("Auth"),
            app.MapGroup("/api/v1/auth").WithTags("Auth")
        };

        foreach (var group in groups)
        {
            // FR-AUTH-001: POST /auth/register trả 201 + UserProfileDto (không có mật khẩu)
            group.MapPost("/register", async (RegisterCommand command, ISender sender) =>
            {
                var result = await sender.Send(command);
                return Results.Created("/auth/me", result);
            });

            // FR-AUTH-002: POST /auth/login return {accessToken, refreshToken} or 401
            group.MapPost("/login", async (LoginRequest request, HttpContext http, ISender sender) =>
            {
                var command = new LoginCommand(request.Email, request.Password, http.Connection.RemoteIpAddress?.ToString());
                var result = await sender.Send(command);
                return Results.Ok(new
                {
                    accessToken = result.AccessToken,
                    refreshToken = result.RefreshToken
                });
            });

            group.MapPost("/refresh", async (RefreshRequest request, HttpContext http, ISender sender) =>
            {
                var command = new RefreshTokenCommand(request.RefreshToken, http.Connection.RemoteIpAddress?.ToString());
                var result = await sender.Send(command);
                return result.ToOkResponse();
            });

            group.MapPost("/logout", async (RefreshRequest request, ISender sender) =>
            {
                await sender.Send(new LogoutCommand(request.RefreshToken));
                return Results.NoContent();
            }).RequireAuthorization();

            group.MapGet("/me", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new GetMeQuery(user.GetUserId()));
                return result.ToOkResponse();
            }).RequireAuthorization();

            group.MapPatch("/me", async (UpdateProfileRequest request, ClaimsPrincipal user, ISender sender) =>
            {
                var command = new UpdateProfileCommand(
                    user.GetUserId(),
                    request.DisplayName,
                    request.AvatarUrl,
                    request.Bio);

                var result = await sender.Send(command);
                return result.ToOkResponse();
            }).RequireAuthorization();
        }
    }

    private record LoginRequest(string Email, string Password);

    private record RefreshRequest(string RefreshToken);

    private record UpdateProfileRequest(string? DisplayName, string? AvatarUrl, string? Bio);
}
