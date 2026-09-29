using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.Commands.ResetPassword;

/// <summary>
/// UserManager.ResetPasswordAsync tự kiểm tra Token còn hạn/đúng chữ ký (Data Protection) trước
/// khi đổi mật khẩu — không cần tự quản lý bảng lưu token như RefreshToken.
/// </summary>
public class ResetPasswordCommandHandler(UserManager<ApplicationUser> userManager)
    : IRequestHandler<ResetPasswordCommand>
{
    private const string InvalidOrExpiredMessage = "Yêu cầu đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";

    public async Task Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Email không tồn tại → báo lỗi CHUNG giống token sai/hết hạn (không tiết lộ email nào
        // có đăng ký — cùng nguyên tắc chống User Enumeration như ForgotPasswordCommandHandler).
        if (user is null)
        {
            throw new UnauthorizedException(InvalidOrExpiredMessage);
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            // "InvalidToken" (sai/hết hạn) → 401 chung chung; lỗi khác (vd mật khẩu mới không đủ
            // mạnh) → 422 kèm chi tiết field, giống các Handler dùng IdentityResult khác.
            if (result.Errors.Any(e => e.Code == "InvalidToken"))
            {
                throw new UnauthorizedException(InvalidOrExpiredMessage);
            }

            throw ToValidationException(result);
        }
    }

    private static ValidationException ToValidationException(IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code, e => e.Description)
            .ToDictionary(g => g.Key, g => g.ToArray());

        return new ValidationException(errors);
    }
}
