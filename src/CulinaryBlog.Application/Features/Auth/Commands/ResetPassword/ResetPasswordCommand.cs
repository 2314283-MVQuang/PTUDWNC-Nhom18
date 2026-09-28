using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Commands.ResetPassword;

/// <summary>
/// Tuần 3 — Auth nâng cao. POST /api/v1/auth/reset-password (Actor: Guest, cầm token nhận được
/// từ email ForgotPassword). Không nhận UserId từ client — xác định user qua Email + Token hợp
/// lệ, giống cách Identity thiết kế cặp (email, token) là đủ để chứng minh quyền sở hữu hộp thư.
/// </summary>
public record ResetPasswordCommand(
    string Email,
    string Token,
    string NewPassword) : IRequest;
