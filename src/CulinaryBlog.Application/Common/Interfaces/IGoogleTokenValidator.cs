namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// FR-AUTH-003: xác thực Google ID Token (Auth.js ở frontend gửi lên sau khi người dùng đăng nhập
/// bằng tài khoản Google) và trả về thông tin định danh cơ bản đã được xác minh chữ ký + audience.
/// Application chỉ biết interface này — implementation thật (dùng SDK Google.Apis.Auth, gọi ra
/// ngoài để lấy public key của Google) đặt ở Infrastructure, cùng nguyên tắc với
/// IEmailService/IJwtService (Application không phụ thuộc SDK bên ngoài).
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>Ném UnauthorizedException nếu token không hợp lệ, hết hạn, sai audience, hoặc email
    /// Google chưa được xác minh.</summary>
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct = default);
}

/// <summary>Thông tin lấy được từ Google ID Token sau khi đã xác minh.</summary>
public record GoogleUserInfo(string Email, bool EmailVerified, string DisplayName, string? AvatarUrl, string GoogleSubjectId);
