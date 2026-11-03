using CulinaryBlog.Application.Features.Users.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Users.Commands.SetUserStatus;

/// <summary>
/// Tuần 9 (Quang) — PATCH /api/v1/users/{id}/status (chỉ Admin): khoá / mở khoá tài khoản.
/// Tài khoản bị khoá (IsActive = false) không đăng nhập, refresh token hay đăng nhập Google được
/// (kiểm tra ở Login/Refresh/GoogleLogin — Tuần 8, Ý). Dữ liệu công thức giữ nguyên.
/// </summary>
public sealed record SetUserStatusCommand(string UserId, bool IsActive) : IRequest<UserSummaryDto>;
