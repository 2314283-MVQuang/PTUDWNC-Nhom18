using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Auth.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.Commands.Register;

/// <summary>
/// FR-AUTH-001. Flow: kiểm tra email chưa tồn tại → tạo ApplicationUser → UserManager.CreateAsync
/// (Identity tự hash PBKDF2, CONS-004) → gán role mặc định "Author" → sinh cặp token → lưu
/// RefreshToken → gửi email chào mừng + email xác nhận địa chỉ email.
/// </summary>
public class RegisterCommandHandler(
    UserManager<ApplicationUser> userManager,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IEmailService emailService)
    : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    private const string DefaultRole = "Author";

    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            throw new ConflictException("Email đã được sử dụng.");
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            DisplayName = request.FullName,
            EmailConfirmed = false,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw ToValidationException(createResult);
        }

        // Role "Author" phải đã tồn tại trong bảng AspNetRoles trước khi gọi dòng dưới, nếu không
        // AddToRoleAsync trả IdentityResult thất bại với lỗi "Role does not exist" — xem
        // RoleSeeder.SeedRolesAsync (Tuần 3, gọi ở Program.cs lúc khởi động, MỌI environment).
        var addToRoleResult = await userManager.AddToRoleAsync(user, DefaultRole);
        if (!addToRoleResult.Succeeded)
        {
            throw ToValidationException(addToRoleResult);
        }

        var roles = await userManager.GetRolesAsync(user);

        var accessToken = jwtService.GenerateAccessToken(user, roles);
        var (rawRefreshToken, refreshTokenHash) = jwtService.GenerateRefreshToken();

        await refreshTokenRepository.AddAsync(
            new RefreshToken
            {
                UserId = user.Id,
                TokenHash = refreshTokenHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            },
            ct);

        await unitOfWork.SaveChangesAsync(ct);

        // FR-JOB-001: TODO (nhóm làm tiếp) — đổi thành Hangfire fire-and-forget khi tích hợp Hangfire.
        // Tạm gọi trực tiếp (đồng bộ) để scaffold chạy được ngay.
        await emailService.SendWelcomeEmailAsync(user.Email!, user.DisplayName, ct);

        // Tuần 3: gửi kèm email xác nhận địa chỉ (ConfirmEmailCommand xử lý lúc user bấm link).
        // Token sinh SAU KHI user đã persist (GenerateEmailConfirmationTokenAsync cần user.Id đã
        // có trong DB, giống cách RefreshToken chỉ tạo được sau khi UserManager.CreateAsync xong).
        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailService.SendEmailConfirmationAsync(user.Email!, user.DisplayName, confirmationToken, ct);

        return new AuthResponseDto(
            accessToken,
            rawRefreshToken,
            jwtService.AccessTokenLifetimeSeconds,
            new UserProfileDto(user.Id, user.Email!, user.DisplayName, user.AvatarUrl, user.Bio, roles));
    }

    private static ValidationException ToValidationException(IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code, e => e.Description)
            .ToDictionary(g => g.Key, g => g.ToArray());

        return new ValidationException(errors);
    }
}
