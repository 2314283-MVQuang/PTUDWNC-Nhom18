using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Commands.ForgotPassword;

/// <summary>
/// Tuần 3 — Auth nâng cao. POST /api/v1/auth/forgot-password (Actor: Guest — chưa đăng nhập,
/// quên mật khẩu nên không thể dùng ChangePasswordCommand).
/// </summary>
public record ForgotPasswordCommand(string Email) : IRequest;
