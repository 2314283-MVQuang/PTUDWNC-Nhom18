using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Auth.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Features.Auth.Commands.Login;

/// <summary>
/// FR-AUTH-002. Dùng SignInManager.CheckPasswordSignInAsync(lockoutOnFailure: true) — Identity
/// tự đếm AccessFailedCount và khóa 15 phút sau 5 lần sai (đã cấu hình Lockout options ở
/// Infrastructure/DependencyInjection.cs), không cần tự viết logic đếm.
/// </summary>
public class LoginCommandHandler(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Không tiết lộ email có tồn tại hay không — chống User Enumeration Attack (mục 5.2).
        if (user is null)
        {
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng.");
        }

        var checkResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (checkResult.IsLockedOut)
        {
            throw new LockedOutException("Tài khoản tạm khóa do đăng nhập sai quá 5 lần. Vui lòng thử lại sau 15 phút.");
        }

        if (!checkResult.Succeeded)
        {
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng.");
        }

        // FR-AUTH-004 A4 / SRS AUTH_ACCOUNT_DISABLED: tài khoản bị Admin vô hiệu hoá thì không cấp
        // token mới (403). Kiểm tra SAU mật khẩu để không lộ trạng thái tài khoản cho người lạ.
        if (!user.IsActive)
        {
            throw new ForbiddenAccessException("Tài khoản đã bị vô hiệu hoá. Vui lòng liên hệ quản trị viên.");
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
