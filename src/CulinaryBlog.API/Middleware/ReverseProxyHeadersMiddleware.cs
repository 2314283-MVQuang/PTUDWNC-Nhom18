using System.Net;
using System.Net.Sockets;

namespace CulinaryBlog.API.Middleware;

/// <summary>
/// Triển khai máy chủ thật (Tuần 10, Quang): API không mở cổng ra Internet nữa, mọi request đi qua
/// Caddy (reverse proxy, HTTPS). Khi đó <c>RemoteIpAddress</c> luôn là IP của container Caddy →
/// rate limit theo IP (đăng nhập 5 lần/phút, bình luận...) bị tính CHUNG cho mọi người dùng.
///
/// Middleware này lấy IP thật của trình duyệt từ header <c>X-Forwarded-For</c> và scheme từ
/// <c>X-Forwarded-Proto</c>, NHƯNG chỉ khi request đến từ địa chỉ mạng nội bộ (mạng Docker,
/// loopback). Request từ Internet gửi header giả sẽ bị bỏ qua. Caddy mặc định ghi đè
/// X-Forwarded-For bằng IP client thật nên lấy phần tử cuối cùng.
///
/// Chỉ bật khi cấu hình <c>ReverseProxy:Enabled = true</c> (biến môi trường ReverseProxy__Enabled
/// trong docker-compose.server.yml). Chạy local/dev không ảnh hưởng.
/// </summary>
public sealed class ReverseProxyHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var peer = context.Connection.RemoteIpAddress;
        if (peer is not null && IsPrivateNetwork(peer))
        {
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var parts = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length > 0 && IPAddress.TryParse(parts[^1], out var client))
                {
                    context.Connection.RemoteIpAddress = client;
                }
            }

            var proto = context.Request.Headers["X-Forwarded-Proto"].ToString();
            if (proto is "https" or "http")
            {
                context.Request.Scheme = proto;
            }
        }

        return next(context);
    }

    /// <summary>Loopback, 10/8, 172.16/12, 192.168/16, IPv6 ULA (fc00::/7) — các dải mạng Docker dùng.</summary>
    public static bool IsPrivateNetwork(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168);
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6 && (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
    }
}
