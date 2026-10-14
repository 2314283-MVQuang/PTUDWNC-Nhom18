using System.Net.Mime;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// FR-JOB-003 (Tuần 5 — Tiến): Endpoint phục vụ sitemap.xml và kích hoạt job sinh sitemap.
/// </summary>
public static class SitemapEndpoints
{
    public static IEndpointRouteBuilder MapSitemapEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Phục vụ file sitemap.xml công khai cho search engines và người dùng
        app.MapGet("/sitemap.xml", async (
            ISitemapService sitemapService,
            CancellationToken ct) =>
        {
            var xml = await sitemapService.GetSitemapXmlAsync(ct);
            return Results.Content(xml, "application/xml; charset=utf-8");
        })
        .WithName("GetSitemapXml")
        .WithSummary("Lấy file sitemap.xml cho SEO (FR-JOB-003)")
        .WithDescription("Trả về danh sách URL của trang chủ, các danh mục và các công thức đã publish theo chuẩn sitemaps.org.")
        .Produces<string>(StatusCodes.Status200OK, contentType: "application/xml")
        .AllowAnonymous();

        // 2. Kích hoạt sinh lại sitemap thủ công (hỗ trợ test và trigger theo yêu cầu)
        app.MapPost("/api/v1/jobs/sitemap/trigger", async (
            GenerateSitemapJob job,
            CancellationToken ct) =>
        {
            await job.ExecuteAsync(ct);
            return Results.Ok(new
            {
                success = true,
                message = "Đã kích hoạt sinh sitemap.xml thành công!",
                timestamp = DateTime.UtcNow
            });
        })
        .WithName("TriggerSitemapJob")
        .WithSummary("Kích hoạt chạy job sinh sitemap ngay lập tức (FR-JOB-003)")
        .WithDescription("Chạy tiến trình sinh sitemap.xml và ping search engine theo yêu cầu.")
        .Produces(StatusCodes.Status200OK)
        .AllowAnonymous();

        return app;
    }
}
