using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// FR-OBS-001 — 3 endpoint health check (SRS mục 3.7 + bảng API trang 66):
///   - /health        : tổng hợp PostgreSQL, Redis và MinIO.
///   - /health/live   : liveness — KHÔNG kiểm tra dependency, luôn 200 khi process còn sống.
///   - /health/ready  : readiness — chỉ PostgreSQL + Redis (tag "ready"); 503 khi một trong hai down.
/// Tuần 8 (Tiến): trả JSON (status, thời gian, từng check) thay vì chữ "Healthy" trơn để Uptime
/// Robot / người vận hành biết dependency nào lỗi; bỏ MinIO khỏi readiness theo đúng SRS.
/// </summary>
public static class HealthEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteJsonAsync,
        });

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteJsonAsync,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteJsonAsync,
        });
    }

    /// <summary>Không đưa exception/connection string ra ngoài — chỉ tên check, trạng thái, mô tả ngắn.</summary>
    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                description = entry.Value.Description ?? (entry.Value.Exception is null ? null : "Không kết nối được."),
            }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
