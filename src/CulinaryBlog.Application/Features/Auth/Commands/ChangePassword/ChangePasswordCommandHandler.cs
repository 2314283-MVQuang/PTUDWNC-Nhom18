using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.Commands.ChangePassword;

/// <summary>
/// Dùng UserManager.ChangePasswordAsync — Identity tự kiểm tra CurrentPassword đúng chưa (hash
/// PBKDF2, CONS-004) trước khi cho đổi, không tự viết logic so sánh hash tay.
/// </summary>
public class ChangePasswordCommandHandler(UserManager<ApplicationUser> userManager)
    : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException(nameof(ApplicationUser), request.UserId);

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            // CurrentPassword sai → Identity trả lỗi "PasswordMismatch", gom chung vào 422 giống
            // các lỗi Identity khác (RegisterCommandHandler) — không dùng 401 vì đây không phải
            // lỗi xác thực JWT, mà là lỗi dữ liệu người dùng nhập sai ở 1 field cụ thể.
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
