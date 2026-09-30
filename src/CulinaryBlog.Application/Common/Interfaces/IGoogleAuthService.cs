namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>Xác minh idToken do Google phát hành (FR-AUTH-003). Ném UnauthorizedException nếu
/// token giả mạo/hết hạn/sai audience.</summary>
public interface IGoogleAuthService
{
    Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken, CancellationToken ct = default);
}

/// <summary>Thông tin lấy được từ idToken sau khi xác minh — Google đã tự kiểm tra email thật (verified).</summary>
public record GoogleUserInfo(string Email, string Name, string? PictureUrl);