using System.Security.Claims;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Auth.Commands.ChangePassword;
using CulinaryBlog.Application.Features.Auth.Commands.ConfirmEmail;
using CulinaryBlog.Application.Features.Auth.Commands.ForgotPassword;
using CulinaryBlog.Application.Features.Auth.Commands.Login;
using CulinaryBlog.Application.Features.Auth.Commands.Logout;
using CulinaryBlog.Application.Features.Auth.Commands.Refresh;
using CulinaryBlog.Application.Features.Auth.Commands.Register;
using CulinaryBlog.Application.Features.Auth.Commands.ResetPassword;
using CulinaryBlog.Application.Features.Auth.Commands.UpdateProfile;
using CulinaryBlog.Application.Features.Auth.Queries.Me;
using CulinaryBlog.Application.Features.Auth.Commands.GoogleLogin;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

/// <summary>Mục 8.1. FR-AUTH-003 (Google OAuth) đã triển khai — xem route /google bên dưới.</summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created("/api/v1/auth/me", new { data = result });
        });

        group.MapPost("/login", async (LoginRequest request, HttpContext http, ISender sender) =>
        {
            var command = new LoginCommand(request.Email, request.Password, http.Connection.RemoteIpAddress?.ToString());
            var result = await sender.Send(command);
            return result.ToOkResponse();
        });

        group.MapPost("/refresh", async (RefreshRequest request, HttpContext http, ISender sender) =>
        {
            var command = new RefreshTokenCommand(request.RefreshToken, http.Connection.RemoteIpAddress?.ToString());
            var result = await sender.Send(command);
            return result.ToOkResponse();
        });

       group.MapPost("/google", async (GoogleRequest request, HttpContext http, ISender sender) =>
        {
            var command = new GoogleLoginCommand(request.IdToken, http.Connection.RemoteIpAddress?.ToString());
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

        // FR-AUTH-007 — chỉ sửa được hồ sơ của CHÍNH MÌNH: UserId lấy từ JWT, không nhận từ body.
        // Field nào không gửi (null) thì giữ nguyên; gửi chuỗi rỗng cho AvatarUrl/Bio = xoá giá trị cũ.
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

        // ---------------------------------------------------------------------------------------
        // Tuần 3 — Auth nâng cao (4 endpoint dưới đây).
        // ---------------------------------------------------------------------------------------

        // Đổi mật khẩu khi ĐÃ đăng nhập (biết mật khẩu hiện tại) — khác forgot/reset (quên mật
        // khẩu, chưa đăng nhập được). UserId lấy từ JWT, không nhận từ body — giống PATCH /me.
        group.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal user, ISender sender) =>
        {
            var command = new ChangePasswordCommand(user.GetUserId(), request.CurrentPassword, request.NewPassword);
            await sender.Send(command);
            return Results.NoContent();
        }).RequireAuthorization();

        // Luôn trả 204 dù email không tồn tại (xem ForgotPasswordCommandHandler) — không tiết lộ
        // email nào có đăng ký trong hệ thống.
        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, ISender sender) =>
        {
            await sender.Send(new ForgotPasswordCommand(request.Email));
            return Results.NoContent();
        });

        group.MapPost("/reset-password", async (ResetPasswordRequest request, ISender sender) =>
        {
            var command = new ResetPasswordCommand(request.Email, request.Token, request.NewPassword);
            await sender.Send(command);
            return Results.NoContent();
        });

        // UserId + Token đến từ link trong email (RegisterCommandHandler gửi lúc đăng ký) — client
        // (frontend) đọc 2 giá trị này từ query string của link rồi POST lên đây.
        group.MapPost("/confirm-email", async (ConfirmEmailRequest request, ISender sender) =>
        {
            await sender.Send(new ConfirmEmailCommand(request.UserId, request.Token));
            return Results.NoContent();
        });
    }

    private record LoginRequest(string Email, string Password);

    private record RefreshRequest(string RefreshToken);
    private record GoogleRequest(string IdToken);

    /// <summary>Body cho PATCH /auth/me — KHÔNG có UserId (chống sửa hồ sơ người khác).</summary>
    private record UpdateProfileRequest(string? DisplayName, string? AvatarUrl, string? Bio);

    /// <summary>Body cho POST /auth/change-password — KHÔNG có UserId (lấy từ JWT).</summary>
    private record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    private record ForgotPasswordRequest(string Email);

    private record ResetPasswordRequest(string Email, string Token, string NewPassword);

    private record ConfirmEmailRequest(string UserId, string Token);
}
