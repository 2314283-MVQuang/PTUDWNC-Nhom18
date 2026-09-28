using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Commands.ConfirmEmail;

/// <summary>
/// Tuần 3 — Auth nâng cao. POST /api/v1/auth/confirm-email (Actor: Guest, bấm link trong email
/// xác nhận gửi lúc đăng ký — xem RegisterCommandHandler). UserId + Token đến từ query string
/// của link email (Endpoint đọc từ query, không phải JWT — lúc này user CHƯA đăng nhập được vì
/// một số luồng có thể yêu cầu xác nhận email trước khi login).
/// </summary>
public record ConfirmEmailCommand(string UserId, string Token) : IRequest;
