using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Commands.Logout;

/// <summary>FR-AUTH-005 — POST /api/v1/auth/logout. Idempotent: trả 204 kể cả khi token không tìm thấy.</summary>
public record LogoutCommand(string RefreshToken, string? UserId = null) : IRequest;
