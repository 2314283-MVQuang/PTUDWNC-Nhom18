using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CulinaryBlog.API.Extensions;

public static class ApiSecurityExtensions
{
    public const string AuthRateLimit = "auth";

    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Thiếu cấu hình Jwt.");
        if (string.IsNullOrWhiteSpace(jwt.Secret) || Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
            throw new InvalidOperationException("Jwt:Secret cần ít nhất 32 byte, cấu hình qua User Secrets hoặc biến môi trường.");
        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
            throw new InvalidOperationException("Jwt:Issuer và Jwt:Audience không được trống.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                ValidateLifetime = true, RequireExpirationTime = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.FromSeconds(30), RoleClaimType = ClaimTypes.Role,
            };
            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    if (context.Response.HasStarted) return;
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    await Results.Problem(statusCode: 401, type: "AUTH_TOKEN_INVALID",
                        title: "Không được xác thực", detail: "Bearer JWT thiếu, không hợp lệ hoặc đã hết hạn.")
                        .ExecuteAsync(context.HttpContext);
                },
                OnForbidden = context => Results.Problem(statusCode: 403, type: "FORBIDDEN",
                    title: "Không có quyền truy cập").ExecuteAsync(context.HttpContext),
            };
        });
        services.AddAuthorization(options => options.AddCulinaryBlogPolicies());

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(origin => origin.Contains('*') || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.UserInfo)))
            throw new InvalidOperationException("Cors:AllowedOrigins chỉ nhận origin HTTP(S) cụ thể, không nhận wildcard/path.");
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .WithHeaders("Authorization", "Content-Type", "Accept", "If-Match", "X-Correlation-ID")
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithExposedHeaders("Retry-After", "ETag", "X-Correlation-ID", "api-supported-versions")
            .AllowCredentials()));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (!context.Request.Path.StartsWithSegments("/api") || HttpMethods.IsOptions(context.Request.Method))
                    return RateLimitPartition.GetNoLimiter("excluded");
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var key = userId is null ? $"ip:{context.Connection.RemoteIpAddress}" : $"user:{userId}";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => Window(100));
            });
            // Chung một ngân sách /auth/* theo IP, không thể lách bằng đổi endpoint/tài khoản.
            options.AddPolicy(AuthRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => Window(10)));
            options.OnRejected = async (context, ct) =>
            {
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 60;
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: 429, type: "RATE_LIMIT_EXCEEDED",
                    title: "Quá nhiều yêu cầu", detail: "Vui lòng thử lại sau khoảng thời gian trong Retry-After.")
                    .ExecuteAsync(context.HttpContext);
            };
        });
        return services;
    }

    private static FixedWindowRateLimiterOptions Window(int limit) => new()
    {
        PermitLimit = limit, Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0, AutoReplenishment = true,
    };
}
