using CulinaryBlog.Application.Features.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Commands.GoogleLogin;

/// <summary>
/// FR-AUTH-003 — POST /api/v1/auth/google (Actor: Guest/Author/Admin). IdToken là Google ID Token
/// (JWT) mà Auth.js (frontend) nhận được từ Google sau khi người dùng chọn tài khoản Google —
/// KHÔNG phải access token OAuth của Google.
/// </summary>
public record GoogleLoginCommand(string IdToken) : IRequest<AuthResponseDto>;
