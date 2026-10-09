using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>
/// FR-JOB-003 (Tuần 5 — Tiến): Hangfire Recurring Job chạy 02:00 AM hàng ngày sinh sitemap.xml
/// và gọi ping Google Search Console theo yêu cầu NFR-SEO.
/// </summary>
public class GenerateSitemapJob(
    ISitemapService sitemapService,
    IConfiguration configuration,
    ILogger<GenerateSitemapJob> logger,
    IHttpClientFactory? httpClientFactory = null)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        logger.LogInformation("[GenerateSitemapJob] Bat dau tien trinh sinh sitemap dinh ky...");

        try
        {
            await sitemapService.GenerateAndSaveAsync(ct);
            logger.LogInformation("[GenerateSitemapJob] Sinh sitemap.xml thanh cong!");

            // Gọi ping Google Search Console theo NFR-SEO & FR-JOB-003
            await PingSearchEnginesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[GenerateSitemapJob] Gap loi khi sinh sitemap.xml");
            throw;
        }
    }

    private async Task PingSearchEnginesAsync(CancellationToken ct)
    {
        try
        {
            var rawBaseUrl = configuration["Frontend:BaseUrl"]
                ?? configuration["Sitemap:BaseUrl"]
                ?? "http://localhost:3000";
            var sitemapUrl = $"{rawBaseUrl.TrimEnd('/')}/sitemap.xml";
            var pingUrl = $"http://www.google.com/ping?sitemap={Uri.EscapeDataString(sitemapUrl)}";

            var client = httpClientFactory?.CreateClient() ?? new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync(pingUrl, ct);
            logger.LogInformation("[GenerateSitemapJob] Da gui ping Google Search Console: StatusCode={StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            // Ping thất bại (vd môi trường dev không có internet hoặc Google chặn) không được làm fail toàn bộ job
            logger.LogWarning("[GenerateSitemapJob] Khong the gui ping toi Search Engine (bo qua trong moi truong dev/offline): {Message}", ex.Message);
        }
    }
}
