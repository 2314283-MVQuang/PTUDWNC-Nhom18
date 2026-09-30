using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Auth.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.Commands.GoogleLogin;

/// <summary>
/// FR-AUTH-003. Flow: xác thực IdToken với Google (IGoogleTokenValidator, implementation thật ở
/// Infrastructure) → tìm user theo email đã xác minh trong token → CHƯA có thì tự tạo tài khoản mới
/// (giống RegisterCommandHandler nhưng KHÔNG cần mật khẩu — tài khoản Google-only, EmailConfirmed =
/// true luôn vì Google đã xác minh sẵn, xem GoogleUserInfo.EmailVerified được kiểm tra ở
/// GoogleTokenValidator) → gán role mặc định "Author" → phát JWT y hệt Login/Register để frontend
/// dùng chung đúng 1 luồng session cho cả 3 cách đăng nhập.
/// </summary>
public class GoogleLoginCommandHandler(
    IGoogleTokenValidator googleTokenValidator,
    UserManager<ApplicationUser> userManager,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<GoogleLoginCommand, AuthResponseDto>
{
    private const string DefaultRole = "Author";

    public async Task<AuthResponseDto> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        var googleUser = await googleTokenValidator.ValidateAsync(request.IdToken, ct);

        var user = await userManager.FindByEmailAsync(googleUser.Email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = googleUser.Email.Split('@')[0],
                Email = googleUser.Email,
                DisplayName = googleUser.DisplayName,
                AvatarUrl = googleUser.AvatarUrl,
                EmailConfirmed = true,
            };

            // Tài khoản Google-only: CreateAsync(user) KHÔNG truyền password vẫn hợp lệ với
            // Identity (PasswordHash để null) — nếu sau này user muốn đặt mật khẩu để đăng nhập
            // thường bằng email/password, dùng UserManager.AddPasswordAsync (ngoài phạm vi FR-AUTH-003).
            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                throw ToValidationException(createResult);
            }

            // Giống RegisterCommandHandler: role "Admin"/"Author" phải đã tồn tại sẵn trong
            // AspNetRoles (RoleSeeder.SeedRolesAsync chạy ở Program.cs lúc khởi động).
            var addToRoleResult = await userManager.AddToRoleAsync(user, DefaultRole);
            if (!addToRoleResult.Succeeded)
            {
                throw ToValidationException(addToRoleResult);
            }
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
