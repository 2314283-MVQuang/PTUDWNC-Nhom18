using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>Cài đặt thật của IGoogleAuthService (khai báo ở Application) — dùng thư viện
/// Google.Apis.Auth để kiểm tra chữ ký số của Google trên idToken.</summary>
public class GoogleAuthService(IOptions<GoogleOptions> options) : IGoogleAuthService
{
    private readonly GoogleOptions _options = options.Value;

    public async Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_options.ClientId], // chỉ chấp nhận token phát cho đúng Client ID của mình
                });

            return new GoogleUserInfo(payload.Email, payload.Name, payload.Picture);
        }
        catch (InvalidJwtException)
        {
            // Token giả mạo, hết hạn, hoặc phát cho một Client ID khác.
            throw new UnauthorizedException("Google idToken không hợp lệ hoặc đã hết hạn.");
        }
    }
}