using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Auth.Commands.ForgotPassword;

/// <summary>
/// Sinh password reset token (Identity Data Protection token, tự hết hạn — mặc định 1 ngày) rồi
/// gửi qua email. Endpoint LUÔN trả về thành công dù email không tồn tại — không tiết lộ email
/// nào có đăng ký trong hệ thống (User Enumeration Attack, cùng nguyên tắc với FR-AUTH-002 ở
/// LoginCommandHandler).
/// </summary>
public class ForgotPasswordCommandHandler(
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    ILogger<ForgotPasswordCommandHandler> logger)
    : IRequestHandler<ForgotPasswordCommand>
{
    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Không throw NotFoundException ở đây — im lặng return coi như đã "gửi email" thành công,
        // để giữ cùng mã phản hồi; không bảo đảm thời gian xử lý giống nhau.
        if (user is null)
        {
            return;
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        try
        {
            await emailService.SendPasswordResetEmailAsync(user.Email!, user.DisplayName, resetToken, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Giữ phản hồi chung kể cả khi SMTP lỗi, tránh tiết lộ email qua mã trạng thái.
            // Không ghi token/link reset hoặc nội dung thư vào log. Người dùng có thể gửi lại.
            logger.LogWarning("Password reset email could not be delivered for user {UserId}.", user.Id);
        }
    }
}
