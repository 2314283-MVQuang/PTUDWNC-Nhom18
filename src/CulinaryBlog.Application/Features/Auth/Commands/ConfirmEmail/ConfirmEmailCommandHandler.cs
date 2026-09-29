using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.Commands.ConfirmEmail;

public class ConfirmEmailCommandHandler(UserManager<ApplicationUser> userManager)
    : IRequestHandler<ConfirmEmailCommand>
{
    public async Task Handle(ConfirmEmailCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException(nameof(ApplicationUser), request.UserId);

        // Idempotent: user bấm lại link cũ (đã xác nhận từ trước) vẫn coi như thành công, không
        // báo lỗi — tránh trải nghiệm khó chịu khi mail client tự "prefetch" link 2 lần.
        if (user.EmailConfirmed)
        {
            return;
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            throw new UnauthorizedException("Liên kết xác nhận email không hợp lệ hoặc đã hết hạn.");
        }
    }
}
