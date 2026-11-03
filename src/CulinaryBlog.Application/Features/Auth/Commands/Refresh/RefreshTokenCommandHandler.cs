using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Auth.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Auth.Commands.Refresh;

/// <summary>
/// FR-AUTH-004 — Token Rotation bắt buộc + Reuse Detection (mục 5.2):
///   1. Tìm RefreshToken theo hash. Không tồn tại hoặc hết hạn → 401.
///   2. ĐÃ bị revoke mà vẫn được dùng lại → dấu hiệu token bị đánh cắp: log SECURITY ALERT
///      và revoke TOÀN BỘ refresh token còn hiệu lực của user ("paranoid mode") → 401.
///   3. Còn hợp lệ → revoke token cũ (đánh dấu ReplacedByTokenHash), sinh cặp token mới hoàn toàn.
/// </summary>
public class RefreshTokenCommandHandler(
    UserManager<ApplicationUser> userManager,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var tokenHash = jwtService.HashRefreshToken(request.RefreshToken);
        var existingToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, ct);

        if (existingToken is null || existingToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new UnauthorizedException("Refresh token không hợp lệ hoặc đã hết hạn.");
        }

        if (existingToken.RevokedAt is not null)
        {
            // Reuse Detection: token này đã bị rotate trước đó nhưng vẫn có người dùng lại →
            // rất có thể refresh token đã bị đánh cắp. Revoke toàn bộ family để chặn kẻ tấn công.
            logger.LogWarning(
                "SECURITY ALERT: phát hiện refresh token reuse cho UserId={UserId}. Revoke toàn bộ token.",
                existingToken.UserId);

            await refreshTokenRepository.RevokeAllActiveTokensForUserAsync(existingToken.UserId, ct);
            await unitOfWork.SaveChangesAsync(ct);

            throw new UnauthorizedException("Refresh token không hợp lệ hoặc đã hết hạn.");
        }

        var user = await userManager.FindByIdAsync(existingToken.UserId)
            ?? throw new UnauthorizedException("Refresh token không hợp lệ hoặc đã hết hạn.");

        // FR-AUTH-004 A4 / SRS AUTH_ACCOUNT_DISABLED: tài khoản bị Admin vô hiệu hoá thì không cấp
        // token mới (403). Kiểm tra SAU mật khẩu để không lộ trạng thái tài khoản cho người lạ.
        if (!user.IsActive)
        {
            throw new ForbiddenAccessException("Tài khoản đã bị vô hiệu hoá. Vui lòng liên hệ quản trị viên.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtService.GenerateAccessToken(user, roles);
        var (newRawToken, newTokenHash) = jwtService.GenerateRefreshToken();

        existingToken.RevokedAt = DateTimeOffset.UtcNow;
        existingToken.ReplacedByTokenHash = newTokenHash;
        refreshTokenRepository.Update(existingToken);

        await refreshTokenRepository.AddAsync(
            new RefreshToken
            {
                UserId = user.Id,
                TokenHash = newTokenHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                CreatedByIp = request.IpAddress,
            },
            ct);

        await unitOfWork.SaveChangesAsync(ct);

        return new AuthResponseDto(
            accessToken,
            newRawToken,
            jwtService.AccessTokenLifetimeSeconds,
            new UserProfileDto(user.Id, user.Email!, user.DisplayName, user.AvatarUrl, user.Bio, roles));
    }
}
