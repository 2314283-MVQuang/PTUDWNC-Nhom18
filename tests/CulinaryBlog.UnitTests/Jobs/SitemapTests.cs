using System.Xml.Linq;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Sitemap.Queries;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CulinaryBlog.UnitTests.Jobs;

/// <summary>
/// FR-JOB-003 (Tuần 5 — Tiến): Unit tests cho chức năng sinh và phục vụ sitemap.xml.
/// </summary>
public class SitemapTests : IDisposable
{
    private readonly string _tempDir;

    public SitemapTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sitemap_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static CulinaryBlogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CulinaryBlogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CulinaryBlogDbContext(options);
    }

    private static IConfiguration CreateConfiguration(string baseUrl = "https://culinaryblog.example.com")
    {
        var dict = new Dictionary<string, string?>
        {
            ["Frontend:BaseUrl"] = baseUrl
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private class TestHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "CulinaryBlog.UnitTests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task BuildAsync_ShouldGenerateValidSitemapXml_WithExpectedUrls()
    {
        // Arrange
        using var db = CreateDbContext();
        var catRepo = new RepositoryBase<Category>(db);
        var recipeRepo = new RepositoryBase<Recipe>(db);
        var config = CreateConfiguration();

        // Thêm Categories: 1 còn hoạt động, 1 đã xoá mềm
        var activeCategory = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Món kho",
            Slug = "mon-kho",
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var deletedCategory = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Món đã xoá",
            Slug = "mon-da-xoa",
            IsDeleted = true,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-15)
        };
        db.Categories.AddRange(activeCategory, deletedCategory);

        // Thêm Recipes: 1 Published, 1 Draft, 1 Archived, 1 Published nhưng IsDeleted
        var author = new ApplicationUser
        {
            Id = "author-1",
            UserName = "author@example.com",
            Email = "author@example.com",
            DisplayName = "Đầu bếp A"
        };
        db.Users.Add(author);

        var publishedRecipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Cá kho tộ",
            Slug = "ca-kho-to",
            Description = "Món cá kho thơm ngon",
            Instructions = "Kho cá với nước màu và tiêu",
            AuthorId = author.Id,
            CategoryId = activeCategory.Id,
            Status = RecipeStatus.Published,
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-5),
            IsDeleted = false
        };

        var draftRecipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Thịt kho tàu (Nháp)",
            Slug = "thit-kho-tau-nhap",
            Description = "Bản nháp",
            Instructions = "Đang soạn",
            AuthorId = author.Id,
            CategoryId = activeCategory.Id,
            Status = RecipeStatus.Draft,
            IsDeleted = false
        };

        var archivedRecipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Canh chua (Lưu trữ)",
            Slug = "canh-chua-luu-tru",
            Description = "Công thức cũ",
            Instructions = "Đã lưu trữ",
            AuthorId = author.Id,
            CategoryId = activeCategory.Id,
            Status = RecipeStatus.Archived,
            IsDeleted = false
        };

        var deletedRecipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Bún bò (Đã xoá)",
            Slug = "bun-bo-da-xoa",
            Description = "Đã xoá mềm",
            Instructions = "Đã xoá",
            AuthorId = author.Id,
            CategoryId = activeCategory.Id,
            Status = RecipeStatus.Published,
            IsDeleted = true
        };

        db.Recipes.AddRange(publishedRecipe, draftRecipe, archivedRecipe, deletedRecipe);
        await db.SaveChangesAsync();

        var builder = new SitemapBuilder(recipeRepo, catRepo, config);

        // Act
        var xmlContent = await builder.BuildAsync();

        // Assert
        Assert.NotNull(xmlContent);
        Assert.NotEmpty(xmlContent);

        // Parse XML để xác minh cấu trúc chuẩn sitemaps.org
        var doc = XDocument.Parse(xmlContent);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urls = doc.Descendants(ns + "url").ToList();

        Assert.NotEmpty(urls);

        var locList = urls.Select(u => u.Element(ns + "loc")?.Value).Where(x => x != null).ToList();

        // Phải có URL trang chủ, danh sách recipes, danh sách categories
        Assert.Contains("https://culinaryblog.example.com/", locList);
        Assert.Contains("https://culinaryblog.example.com/recipes", locList);
        Assert.Contains("https://culinaryblog.example.com/categories", locList);

        // Phải có Category đang hoạt động
        Assert.Contains("https://culinaryblog.example.com/categories/mon-kho", locList);
        // KHÔNG được có Category đã bị xoá
        Assert.DoesNotContain("https://culinaryblog.example.com/categories/mon-da-xoa", locList);

        // Phải có Recipe đã Publish
        Assert.Contains("https://culinaryblog.example.com/recipes/ca-kho-to", locList);
        // KHÔNG được chứa Draft, Archived, hoặc Recipe đã xoá
        Assert.DoesNotContain("https://culinaryblog.example.com/recipes/thit-kho-tau-nhap", locList);
        Assert.DoesNotContain("https://culinaryblog.example.com/recipes/canh-chua-luu-tru", locList);
        Assert.DoesNotContain("https://culinaryblog.example.com/recipes/bun-bo-da-xoa", locList);
    }

    [Fact]
    public async Task SitemapService_GenerateAndSave_ShouldCreateFileAndCache()
    {
        // Arrange
        using var db = CreateDbContext();
        var catRepo = new RepositoryBase<Category>(db);
        var recipeRepo = new RepositoryBase<Recipe>(db);
        var config = CreateConfiguration();
        var builder = new SitemapBuilder(recipeRepo, catRepo, config);

        var env = new TestHostEnvironment(_tempDir);
        var logger = NullLogger<SitemapService>.Instance;
        var service = new SitemapService(builder, env, logger);

        // Act: Generate and Save
        await service.GenerateAndSaveAsync();

        // Assert: File tồn tại trong wwwroot/sitemap.xml
        var expectedFilePath = Path.Combine(_tempDir, "wwwroot", "sitemap.xml");
        Assert.True(File.Exists(expectedFilePath));

        var fileContent = await File.ReadAllTextAsync(expectedFilePath);
        Assert.Contains("<urlset", fileContent);
        Assert.Contains("http://www.sitemaps.org/schemas/sitemap/0.9", fileContent);

        // Act: GetSitemapXmlAsync trả về đúng nội dung
        var fetchedXml = await service.GetSitemapXmlAsync();
        Assert.Equal(fileContent, fetchedXml);
    }

    [Fact]
    public async Task GetSitemapQueryHandler_ShouldReturnXmlFromService()
    {
        // Arrange
        var mockService = new FakeSitemapService("<urlset><url><loc>https://example.com/</loc></url></urlset>");
        var handler = new GetSitemapQueryHandler(mockService);

        // Act
        var result = await handler.Handle(new GetSitemapQuery(), CancellationToken.None);

        // Assert
        Assert.Equal("<urlset><url><loc>https://example.com/</loc></url></urlset>", result);
    }

    [Fact]
    public async Task GenerateSitemapJob_ExecuteAsync_ShouldCallGenerateAndSave()
    {
        // Arrange
        var fakeService = new FakeSitemapService("<urlset></urlset>");
        var config = CreateConfiguration();
        var logger = NullLogger<GenerateSitemapJob>.Instance;

        var job = new GenerateSitemapJob(fakeService, config, logger, null);

        // Act
        await job.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.True(fakeService.GenerateCalled);
    }

    private class FakeSitemapService(string xml) : ISitemapService
    {
        public bool GenerateCalled { get; private set; }

        public Task<string> GetSitemapXmlAsync(CancellationToken ct = default)
        {
            return Task.FromResult(xml);
        }

        public Task GenerateAndSaveAsync(CancellationToken ct = default)
        {
            GenerateCalled = true;
            return Task.CompletedTask;
        }
    }
}
