using System.Globalization;
using System.Xml.Linq;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>
/// FR-JOB-003 (Tuần 5 — Tiến): Cài đặt ISitemapBuilder sinh sitemap XML chuẩn sitemaps.org (0.9).
/// </summary>
public class SitemapBuilder(
    IRepository<Recipe> recipeRepository,
    IRepository<Category> categoryRepository,
    IConfiguration configuration) : ISitemapBuilder
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public async Task<string> BuildAsync(CancellationToken ct = default)
    {
        var rawBaseUrl = configuration["Frontend:BaseUrl"]
            ?? configuration["Sitemap:BaseUrl"]
            ?? configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault()
            ?? "http://localhost:3000";

        var baseUrl = rawBaseUrl.TrimEnd('/');
        var nowStr = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var urlset = new XElement(Ns + "urlset");

        // 1. Trang chủ
        urlset.Add(CreateUrlElement($"{baseUrl}/", nowStr, "daily", "1.0"));

        // 2. Trang danh sách công thức
        urlset.Add(CreateUrlElement($"{baseUrl}/recipes", nowStr, "daily", "0.9"));

        // 3. Trang danh sách danh mục
        urlset.Add(CreateUrlElement($"{baseUrl}/categories", nowStr, "weekly", "0.8"));

        try
        {
            // 4. Các danh mục (chỉ lấy danh mục chưa xóa)
            var categories = await categoryRepository.Query()
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.OrderIndex)
                .ToListAsync(ct);

            foreach (var cat in categories)
            {
                var lastMod = (cat.UpdatedAt ?? cat.CreatedAt).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                urlset.Add(CreateUrlElement($"{baseUrl}/categories/{cat.Slug}", lastMod, "weekly", "0.8"));
            }

            // 5. Các công thức đã Publish (chưa xóa và Status == Published)
            var publishedRecipes = await recipeRepository.Query()
                .Where(r => !r.IsDeleted && r.Status == RecipeStatus.Published)
                .OrderByDescending(r => r.PublishedAt)
                .ToListAsync(ct);

            foreach (var recipe in publishedRecipes)
            {
                var lastMod = (recipe.PublishedAt ?? recipe.UpdatedAt ?? recipe.CreatedAt).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                urlset.Add(CreateUrlElement($"{baseUrl}/recipes/{recipe.Slug}", lastMod, "daily", "0.9"));
            }
        }
        catch (Exception)
        {
            // Trường hợp môi trường dev/kiểm thử không có kết nối cơ sở dữ liệu, sitemap vẫn duy trì các URL cốt lõi
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            urlset
        );

        using var sw = new StringWriter();
        doc.Save(sw);
        return sw.ToString();
    }

    private static XElement CreateUrlElement(string loc, string lastmod, string changefreq, string priority)
    {
        return new XElement(Ns + "url",
            new XElement(Ns + "loc", loc),
            new XElement(Ns + "lastmod", lastmod),
            new XElement(Ns + "changefreq", changefreq),
            new XElement(Ns + "priority", priority)
        );
    }
}
