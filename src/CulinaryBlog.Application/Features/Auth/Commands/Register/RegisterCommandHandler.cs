using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Auth.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Auth.Commands.Register;

/// <summary>
/// FR-AUTH-001. Flow: kiểm tra email chưa tồn tại → tạo ApplicationUser (UserName tự lấy từ phần
/// trước "@" của email nếu client không gửi — RegisterCommand.UserName là optional) →
/// UserManager.CreateAsync (Identity tự hash PBKDF2, CONS-004) → gán role mặc định "Author" →
/// sinh cặp token (đăng ký xong đăng nhập luôn, không bắt gọi thêm /login) → lưu RefreshToken →
/// lên lịch WelcomeEmailJob + giữ luồng email xác nhận địa chỉ hiện có.
/// </summary>
public class RegisterCommandHandler(
    UserManager<ApplicationUser> userManager,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IEmailService emailService,
    IWelcomeEmailScheduler welcomeEmails,
    ILogger<RegisterCommandHandler> logger)
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

        // UserName optional: nếu client không gửi, tự lấy phần trước "@" của email — tránh bắt
        // người dùng nghĩ thêm 1 tên đăng nhập riêng lúc đăng ký.
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

        // Role "Author" phải đã tồn tại trong bảng AspNetRoles trước khi gọi dòng dưới, nếu không
        // AddToRoleAsync trả IdentityResult thất bại với lỗi "Role does not exist" — xem
        // RoleSeeder.SeedRolesAsync (Tuần 3, gọi ở Program.cs lúc khởi động, MỌI environment).
        // KHÔNG tự tạo role ở đây (không inject RoleManager vào Handler này) — RoleSeeder là nơi
        // DUY NHẤT được phép tạo role, tránh 2 chỗ cùng có quyền ghi vào AspNetRoles.
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

        // FR-JOB-001: chỉ enqueue SAU SaveChanges; worker gửi SMTP ngoài request đăng ký.
        try
        {
            var jobId = welcomeEmails.Enqueue(user.Id);
            logger.LogInformation("Welcome email job {JobId} queued for user {UserId}.", jobId, user.Id);
        }
        catch (Exception ex)
        {
            // Tài khoản đã được lưu. Ghi lỗi để có thể lên lịch lại; không báo đăng ký thất bại giả.
            logger.LogError(ex, "Could not enqueue welcome email for user {UserId}.", user.Id);
        }

        try
        {
            // Tuần 3: gửi kèm email xác nhận địa chỉ (ConfirmEmailCommand xử lý lúc user bấm link).
            var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await emailService.SendEmailConfirmationAsync(user.Email!, user.DisplayName, confirmationToken, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send confirmation email for user {UserId}.", user.Id);
        }

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
