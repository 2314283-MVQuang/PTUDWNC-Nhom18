using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// FR-OBS-001 (Tuần 3) — 3 endpoint health check, đúng quy ước phổ biến cho container
/// orchestrator (Docker healthcheck, Kubernetes liveness/readiness probe):
///   - /health        : tổng hợp TẤT CẢ health check đã đăng ký (hiện chỉ có PostgreSQL).
///   - /health/live    : app còn sống không — KHÔNG kiểm tra dependency ngoài, chỉ xác nhận
///                       process ASP.NET Core còn nhận request được. Orchestrator dùng cái này
///                       để quyết định có cần RESTART container không.
///   - /health/ready   : app đã sẵn sàng nhận traffic thật chưa — CÓ kiểm tra dependency (hiện
///                       tại: PostgreSQL, tag "ready" — xem Infrastructure/DependencyInjection.cs).
///                       Orchestrator dùng cái này để quyết định có nên ROUTE traffic vào
///                       instance này không (chưa sẵn sàng thì bỏ qua, không restart).
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health");

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });
    }
}
