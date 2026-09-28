using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Commands.ChangePassword;

/// <summary>
/// Tuần 3 — Auth nâng cao. POST /api/v1/auth/change-password (Actor: user đã đăng nhập).
/// Khác ForgotPassword/ResetPassword: người dùng ĐANG đăng nhập và biết mật khẩu hiện tại,
/// không cần token gửi qua email. UserId lấy từ JWT ở Endpoint, không nhận từ body (giống
/// UpdateProfileCommand) — không ai đổi được mật khẩu người khác.
/// </summary>
public record ChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword) : IRequest;
