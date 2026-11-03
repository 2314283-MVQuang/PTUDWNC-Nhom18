using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Commands.Logout;

/// <summary>
/// FR-AUTH-005: JWT access token stateless nên không thể revoke trực tiếp — client tự xóa khỏi
/// bộ nhớ. Server chỉ revoke refresh token tương ứng trong DB.
/// </summary>
public class LogoutCommandHandler(
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        var tokenHash = jwtService.HashRefreshToken(request.RefreshToken);
        var existingToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, ct);

        // Idempotent: không tìm thấy vẫn coi như thành công, không tiết lộ trạng thái.
        // Tuần 8: chỉ thu hồi refresh token của CHÍNH người đang đăng nhập — không cho dùng JWT của
        // mình để đăng xuất phiên của người khác khi lấy được refresh token của họ.
        if (existingToken is not null
            && existingToken.RevokedAt is null
            && (request.UserId is null || existingToken.UserId == request.UserId))
        {
            existingToken.RevokedAt = DateTimeOffset.UtcNow;
            refreshTokenRepository.Update(existingToken);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
