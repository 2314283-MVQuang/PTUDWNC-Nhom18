using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Auth.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.Commands.GoogleLogin;

/// <summary>
/// FR-AUTH-003. Flow: xác minh idToken với Google → tìm user theo email → có thì đăng nhập
/// luôn (tự liên kết với tài khoản email/password cùng email nếu có), chưa có thì tạo mới
/// (không cần mật khẩu, EmailConfirmed = true vì Google đã xác thực email thật) → phát JWT +
/// refresh token giống hệt Login/Register.
/// </summary>
public class GoogleLoginCommandHandler(
    IGoogleAuthService googleAuthService,
    UserManager<ApplicationUser> userManager,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<GoogleLoginCommand, AuthResponseDto>
{
    private const string DefaultRole = "Author";

    public async Task<AuthResponseDto> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        var googleUser = await googleAuthService.ValidateIdTokenAsync(request.IdToken, ct);

        var user = await userManager.FindByEmailAsync(googleUser.Email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = googleUser.Email,
                Email = googleUser.Email,
                DisplayName = googleUser.Name,
                AvatarUrl = googleUser.PictureUrl,
                EmailConfirmed = true, // Google đã xác thực email thay chúng ta rồi
            };

            var createResult = await userManager.CreateAsync(user); // không kèm password
            if (!createResult.Succeeded)
            {
                var errors = createResult.Errors
                    .GroupBy(e => e.Code, e => e.Description)
                    .ToDictionary(g => g.Key, g => g.ToArray());
                throw new Common.Exceptions.ValidationException(errors);
            }

            await userManager.AddToRoleAsync(user, DefaultRole);
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
                CreatedByIp = request.IpAddress,
            },
            ct);

        await unitOfWork.SaveChangesAsync(ct);

        return new AuthResponseDto(
            accessToken,
            rawRefreshToken,
            jwtService.AccessTokenLifetimeSeconds,
            new UserProfileDto(user.Id, user.Email!, user.DisplayName, user.AvatarUrl, user.Bio, roles));
    }
}