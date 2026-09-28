using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.Commands.ForgotPassword;

/// <summary>
/// Sinh password reset token (Identity Data Protection token, tự hết hạn — mặc định 1 ngày) rồi
/// gửi qua email. Endpoint LUÔN trả về thành công dù email không tồn tại — không tiết lộ email
/// nào có đăng ký trong hệ thống (User Enumeration Attack, cùng nguyên tắc với FR-AUTH-002 ở
/// LoginCommandHandler).
/// </summary>
public class ForgotPasswordCommandHandler(
    UserManager<ApplicationUser> userManager,
    IEmailService emailService)
    : IRequestHandler<ForgotPasswordCommand>
{
    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Không throw NotFoundException ở đây — im lặng return coi như đã "gửi email" thành công,
        // để response giống hệt trường hợp email tồn tại (chống dò email qua thời gian phản hồi
        // hoặc mã lỗi khác nhau).
        if (user is null)
        {
            return;
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        await emailService.SendPasswordResetEmailAsync(user.Email!, user.DisplayName, resetToken, ct);
    }
}
