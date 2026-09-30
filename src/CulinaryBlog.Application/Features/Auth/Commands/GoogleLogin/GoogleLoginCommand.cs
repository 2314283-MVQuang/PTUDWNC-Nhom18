using CulinaryBlog.Application.Features.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Commands.GoogleLogin;

/// <summary>FR-AUTH-003 — POST /api/v1/auth/google.</summary>
public record GoogleLoginCommand(string IdToken, string? IpAddress = null) : IRequest<AuthResponseDto>;