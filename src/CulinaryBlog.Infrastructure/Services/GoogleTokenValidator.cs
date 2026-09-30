using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>
/// FR-AUTH-003: xác thực Google ID Token bằng thư viện chính thức Google.Apis.Auth —
/// GoogleJsonWebSignature.ValidateAsync tự tải public key của Google (JWKS, có cache nội bộ) để
/// kiểm tra chữ ký + hạn dùng + audience (Client ID) của token, KHÔNG tự parse/giải mã JWT thủ công
/// (dễ sai và dễ bị giả mạo token nếu chỉ decode mà không verify signature).
///
/// "Authentication:Google:ClientId" PHẢI trùng với AUTH_GOOGLE_ID bên frontend (.env.local) — đây
/// chính là "audience" mà Google gắn vào token lúc phát hành, sai giá trị này thì token hợp lệ vẫn
/// bị từ chối.
/// </summary>
public class GoogleTokenValidator(IConfiguration configuration) : IGoogleTokenValidator
{
    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        var clientId = configuration["Authentication:Google:ClientId"]
            ?? throw new InvalidOperationException(
                "Thiếu cấu hình Authentication:Google:ClientId (phải trùng AUTH_GOOGLE_ID bên frontend).");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedException("Google ID Token không hợp lệ hoặc đã hết hạn.");
        }

        if (!payload.EmailVerified)
        {
            throw new UnauthorizedException("Email Google chưa được xác minh.");
        }

        return new GoogleUserInfo(
            payload.Email,
            payload.EmailVerified,
            string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name,
            payload.Picture,
            payload.Subject);
    }
}
