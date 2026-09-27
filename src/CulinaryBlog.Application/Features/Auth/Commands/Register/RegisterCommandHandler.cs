using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Auth.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.Commands.Register;

/// <summary>
/// FR-AUTH-001 (Buổi 2).
/// Kiểm tra email chưa tồn tại -> tạo ApplicationUser -> UserManager.CreateAsync (PBKDF2)
/// -> gán role "Author" -> trả về UserProfileDto (không có mật khẩu).
/// </summary>
public class RegisterCommandHandler(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IUnitOfWork unitOfWork,
    IEmailService emailService)
    : IRequestHandler<RegisterCommand, UserProfileDto>
{
    private const string DefaultRole = "Author";

    public async Task<UserProfileDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            throw new ConflictException("Email đã được sử dụng.");
        }

        var userName = !string.IsNullOrWhiteSpace(request.UserName)
            ? request.UserName
            : request.Email.Split('@')[0];

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = request.Email,
            DisplayName = request.FullName,
            EmailConfirmed = false,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw ToValidationException(createResult);
        }

        if (!await roleManager.RoleExistsAsync(DefaultRole))
        {
            await roleManager.CreateAsync(new IdentityRole(DefaultRole));
        }

        await userManager.AddToRoleAsync(user, DefaultRole);
        var roles = await userManager.GetRolesAsync(user);

        await unitOfWork.SaveChangesAsync(ct);

        // Gửi email chào mừng
        try
        {
            await emailService.SendWelcomeEmailAsync(user.Email!, user.DisplayName, ct);
        }
        catch
        {
            // Không để lỗi gửi email ảnh hưởng kết quả đăng ký
        }

        return new UserProfileDto(user.Id, user.Email!, user.DisplayName, user.AvatarUrl, user.Bio, roles);
    }

    private static ValidationException ToValidationException(IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code, e => e.Description)
            .ToDictionary(g => g.Key, g => g.ToArray());

        return new ValidationException(errors);
    }
}
