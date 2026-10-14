using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>
/// FR-JOB-003 (Tuần 5 — Tiến): Service quản lý lưu trữ và phục vụ sitemap.xml.
/// </summary>
public class SitemapService(
    ISitemapBuilder sitemapBuilder,
    IHostEnvironment environment,
    ILogger<SitemapService> logger) : ISitemapService
{
    private static string? _cachedXml;
    private static DateTime _lastGenerated = DateTime.MinValue;
    private static readonly SemaphoreSlim Lock = new(1, 1);

    private string GetFilePath()
    {
        var webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");
        if (!Directory.Exists(webRoot))
        {
            Directory.CreateDirectory(webRoot);
        }
        return Path.Combine(webRoot, "sitemap.xml");
    }

    public async Task<string> GetSitemapXmlAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(_cachedXml))
        {
            return _cachedXml;
        }

        var filePath = GetFilePath();
        if (File.Exists(filePath))
        {
            try
            {
                _cachedXml = await File.ReadAllTextAsync(filePath, ct);
                return _cachedXml;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Khong the doc file sitemap.xml tu dia, se tien hanh sinh moi.");
            }
        }

        // Sinh mới nếu chưa có
        await GenerateAndSaveAsync(ct);
        return _cachedXml ?? await sitemapBuilder.BuildAsync(ct);
    }

    public async Task GenerateAndSaveAsync(CancellationToken ct = default)
    {
        await Lock.WaitAsync(ct);
        try
        {
            logger.LogInformation("[SitemapService] Dang sinh sitemap.xml moi...");
            var xml = await sitemapBuilder.BuildAsync(ct);
            _cachedXml = xml;
            _lastGenerated = DateTime.UtcNow;

            var filePath = GetFilePath();
            await File.WriteAllTextAsync(filePath, xml, ct);
            logger.LogInformation("[SitemapService] Da sinh va luu sitemap.xml thanh cong tai {Path} ({Length} bytes)", filePath, xml.Length);
        }
        finally
        {
            Lock.Release();
        }
    }
}
