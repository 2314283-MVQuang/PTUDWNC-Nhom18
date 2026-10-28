using System.Security.Claims;
using Hangfire.Dashboard;

namespace CulinaryBlog.API.Security;

/// <summary>
/// Tuần 8 (Tiến) — SRS mục 3.6 / 5.3: "Dashboard /hangfire (Admin only, policy-protected)".
/// Trước đây <c>Authorization = []</c> nên ai biết URL cũng xem/xoá/chạy lại job được.
///
/// Dashboard mở bằng trình duyệt nên không tự gửi header Bearer. Cách dùng:
///   1. Đăng nhập tài khoản Admin, lấy accessToken (POST /api/v1/auth/login).
///   2. Mở <c>/hangfire?access_token=&lt;token&gt;</c> — JwtBearer đọc token từ query (chỉ với đường
///      dẫn /hangfire, xem <see cref="HangfireDashboardToken"/>) rồi lưu vào cookie HttpOnly
///      path=/hangfire để các request AJAX của dashboard vẫn được xác thực.
/// Không phải Admin → Hangfire trả 401 (chưa đăng nhập) / 403 (đăng nhập nhưng không phải Admin).
/// </summary>
public sealed class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => IsAdmin(context.GetHttpContext().User);

    public static bool IsAdmin(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true && user.IsInRole("Admin");
}

/// <summary>Đọc JWT cho riêng dashboard Hangfire từ query <c>access_token</c> hoặc cookie.</summary>
public static class HangfireDashboardToken
{
    public const string PathPrefix = "/hangfire";
    public const string QueryName = "access_token";
    public const string CookieName = "hangfire_access_token";

    public static bool IsDashboardRequest(HttpRequest request) => request.Path.StartsWithSegments(PathPrefix);

    /// <summary>Token cho request dashboard; null nếu không phải /hangfire hoặc không có token.</summary>
    public static string? Read(HttpRequest request)
    {
        if (!IsDashboardRequest(request)) return null;
        var fromQuery = request.Query[QueryName].ToString();
        if (!string.IsNullOrWhiteSpace(fromQuery)) return fromQuery;
        return request.Cookies.TryGetValue(CookieName, out var fromCookie) && !string.IsNullOrWhiteSpace(fromCookie)
            ? fromCookie
            : null;
    }

    /// <summary>Gọi sau khi token hợp lệ: lưu token từ query vào cookie HttpOnly chỉ dùng cho /hangfire.</summary>
    public static void PersistFromQuery(HttpContext context, DateTimeOffset? expires)
    {
        var fromQuery = context.Request.Query[QueryName].ToString();
        if (!IsDashboardRequest(context.Request) || string.IsNullOrWhiteSpace(fromQuery)) return;
        context.Response.Cookies.Append(CookieName, fromQuery, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = PathPrefix,
            Expires = expires,
        });
    }
}
