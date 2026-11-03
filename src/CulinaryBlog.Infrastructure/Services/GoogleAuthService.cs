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
        if (string.IsNullOrWhiteSpace(_options.ClientId))
            throw new UnauthorizedException("Đăng nhập Google chưa được cấu hình trên máy chủ.");

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_options.ClientId], // chỉ chấp nhận token phát cho đúng Client ID của mình
                });

            // Tuần 8: chỉ tin email Google ĐÃ xác minh — GoogleLoginCommandHandler gộp tài khoản theo
            // email, nhận email chưa xác minh thì người khác có thể chiếm tài khoản sẵn có.
            if (!payload.EmailVerified)
            {
                throw new UnauthorizedException("Email của tài khoản Google chưa được xác minh.");
            }

            return new GoogleUserInfo(payload.Email, payload.Name, payload.Picture);
        }
        catch (InvalidJwtException)
        {
            // Token giả mạo, hết hạn, hoặc phát cho một Client ID khác.
            throw new UnauthorizedException("Google idToken không hợp lệ hoặc đã hết hạn.");
        }
    }
}
