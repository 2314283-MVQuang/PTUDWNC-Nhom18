namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// FR-JOB-003 (Tuần 5 — Tiến): Interface sinh nội dung XML cho sitemap theo chuẩn sitemaps.org.
/// </summary>
public interface ISitemapBuilder
{
    /// <summary>
    /// Sinh chuỗi XML sitemap chứa trang chủ, các danh mục và các công thức đã publish.
    /// </summary>
    Task<string> BuildAsync(CancellationToken ct = default);
}
