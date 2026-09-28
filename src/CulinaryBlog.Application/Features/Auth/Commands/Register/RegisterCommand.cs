using CulinaryBlog.Application.Features.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Commands.Register;

/// <summary>FR-AUTH-001 — POST /api/v1/auth/register (Actor: Guest).</summary>
public record RegisterCommand(
    string FullName,
    string Email,
    string? UserName,
    string Password) : IRequest<AuthResponseDto>;
