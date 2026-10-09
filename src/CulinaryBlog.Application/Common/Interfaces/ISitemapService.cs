namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// FR-JOB-003 (Tuần 5 — Tiến): Service quản lý sitemap, cung cấp sitemap XML và kích hoạt sinh mới.
/// </summary>
public interface ISitemapService
{
    /// <summary>
    /// Lấy chuỗi XML sitemap hiện tại (từ cache/file hoặc sinh mới nếu chưa có).
    /// </summary>
    Task<string> GetSitemapXmlAsync(CancellationToken ct = default);

    /// <summary>
    /// Kích hoạt sinh lại sitemap và lưu vào file/cache.
    /// </summary>
    Task GenerateAndSaveAsync(CancellationToken ct = default);
}
