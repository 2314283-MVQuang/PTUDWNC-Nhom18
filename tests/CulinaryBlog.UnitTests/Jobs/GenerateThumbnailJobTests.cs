using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Services;
using CulinaryBlog.UnitTests.Recipes;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CulinaryBlog.UnitTests.Jobs;

public class MockThumbnailFileStorageService : IFileStorageService
{
    public string BucketName => "culinaryblog";
    public Dictionary<string, byte[]> Files { get; } = new();
    public bool FailUploads { get; set; }

    public Task<string> GeneratePresignedUploadUrlAsync(string key, string contentType, int expirySeconds = 900, CancellationToken ct = default)
        => Task.FromResult($"https://storage.local/{BucketName}/{key}");

    public Task<Stream> GetFileStreamAsync(string key, CancellationToken ct = default)
    {
        if (Files.TryGetValue(key, out var bytes))
        {
            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }
        throw new FileNotFoundException($"Key not found: {key}");
    }

    public Task<string> UploadFileAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        if (FailUploads) throw new IOException("MinIO không phản hồi");
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        Files[key] = ms.ToArray();
        return Task.FromResult(GetPublicUrl(key));
    }

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        Files.Remove(objectName);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key) => $"https://storage.local/{BucketName}/{key}";
}

public class MockImageProcessor : IImageProcessor
{
    public List<(int Width, int Height)> Calls { get; } = [];

    public byte[] Resize(byte[] inputBytes, int maxWidth, int maxHeight)
    {
        Calls.Add((maxWidth, maxHeight));
        return [.. inputBytes, (byte)(maxWidth % 256), (byte)(maxHeight % 256)];
    }

    public Task<Stream> ResizeAsync(Stream inputStream, int maxWidth, int maxHeight, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        inputStream.CopyTo(ms);
        return Task.FromResult<Stream>(new MemoryStream(Resize(ms.ToArray(), maxWidth, maxHeight)));
    }
}

/// <summary>FR-JOB-002 (Tuần 7 — Tiến): job sinh medium 800×600 và thumbnail 300×300 (SRS trang 38).</summary>
public class GenerateThumbnailJobTests
{
    private const string StorageKey = "uploads/2026/10/abc_pho.png";

    private static CulinaryBlogDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<CulinaryBlogDbContext>().UseInMemoryDatabase(name).Options);

    private static async Task<Guid> SeedImageAsync(string dbName, string originalUrl, bool withUploadedFile = true)
    {
        await using var db = CreateDb(dbName);
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(), Title = "Phở Bò", Slug = $"pho-bo-{Guid.NewGuid():N}", Description = "Phở",
            AuthorId = "user1", CookTime = 30, PrepTime = 15, Servings = 4,
        };
        var image = new RecipeImage { Id = Guid.NewGuid(), RecipeId = recipe.Id, OriginalUrl = originalUrl, OrderIndex = 0 };
        db.Recipes.Add(recipe);
        db.RecipeImages.Add(image);
        if (withUploadedFile)
        {
            db.UploadedFiles.Add(new UploadedFile
            {
                FileName = "pho.png", ContentType = "image/png", Size = 4, StorageKey = StorageKey,
                Url = originalUrl, BucketName = "culinaryblog",
            });
        }
        await db.SaveChangesAsync();
        return image.Id;
    }

    private static GenerateThumbnailJob CreateJob(
        CulinaryBlogDbContext db, MockThumbnailFileStorageService storage, MockImageProcessor processor, TestCacheInvalidator cache) =>
        new(db, processor, storage, NullLogger<GenerateThumbnailJob>.Instance, cache);

    [Fact]
    public async Task ExecuteAsync_ShouldCreateMediumAndThumbnail_AndUpdateUrls()
    {
        var dbName = Guid.NewGuid().ToString();
        var storage = new MockThumbnailFileStorageService();
        storage.Files[StorageKey] = [10, 20, 30, 40];
        var imageId = await SeedImageAsync(dbName, storage.GetPublicUrl(StorageKey));
        var processor = new MockImageProcessor();
        var cache = new TestCacheInvalidator();

        await using (var db = CreateDb(dbName))
        {
            await CreateJob(db, storage, processor, cache).ExecuteAsync(imageId);
        }

        Assert.Contains((800, 600), processor.Calls);
        Assert.Contains((300, 300), processor.Calls);
        // Bản resize lưu JPEG nên dùng đuôi .jpg, không giữ .png của ảnh gốc.
        Assert.True(storage.Files.ContainsKey("uploads/2026/10/abc_pho_medium.jpg"));
        Assert.True(storage.Files.ContainsKey("uploads/2026/10/abc_pho_thumb.jpg"));

        await using var check = CreateDb(dbName);
        var image = await check.RecipeImages.SingleAsync(x => x.Id == imageId);
        Assert.Equal(storage.GetPublicUrl("uploads/2026/10/abc_pho_medium.jpg"), image.MediumUrl);
        Assert.Equal(storage.GetPublicUrl("uploads/2026/10/abc_pho_thumb.jpg"), image.ThumbnailUrl);
        var file = await check.UploadedFiles.SingleAsync();
        Assert.Equal(image.MediumUrl, file.MediumUrl);
        Assert.Equal(image.ThumbnailUrl, file.ThumbnailUrl);
        Assert.Contains($"recipe-{image.RecipeId}", cache.EvictedTags);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowForRetry_AndNotWriteUrls_WhenUploadFails()
    {
        var dbName = Guid.NewGuid().ToString();
        var storage = new MockThumbnailFileStorageService { FailUploads = true };
        storage.Files[StorageKey] = [10, 20, 30, 40];
        var imageId = await SeedImageAsync(dbName, storage.GetPublicUrl(StorageKey));

        await using (var db = CreateDb(dbName))
        {
            await Assert.ThrowsAsync<IOException>(
                () => CreateJob(db, storage, new MockImageProcessor(), new TestCacheInvalidator()).ExecuteAsync(imageId));
        }

        // SRS: lỗi thì ảnh gốc vẫn hiển thị — không ghi URL trỏ tới file không tồn tại.
        await using var check = CreateDb(dbName);
        var image = await check.RecipeImages.SingleAsync(x => x.Id == imageId);
        Assert.Null(image.MediumUrl);
        Assert.Null(image.ThumbnailUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSkip_WhenImageIsOutsideOwnBucket()
    {
        // Không tải ảnh qua HTTP theo URL người dùng gửi (chống SSRF) — URL ngoài bucket thì bỏ qua.
        var dbName = Guid.NewGuid().ToString();
        var storage = new MockThumbnailFileStorageService();
        var imageId = await SeedImageAsync(dbName, "http://169.254.169.254/latest/meta-data/x.png", withUploadedFile: false);
        var processor = new MockImageProcessor();

        await using (var db = CreateDb(dbName))
        {
            await CreateJob(db, storage, processor, new TestCacheInvalidator()).ExecuteAsync(imageId);
        }

        Assert.Empty(processor.Calls);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSkipGracefully_WhenImageNotFound()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        var processor = new MockImageProcessor();

        await CreateJob(db, new MockThumbnailFileStorageService(), processor, new TestCacheInvalidator())
            .ExecuteAsync(Guid.NewGuid());

        Assert.Empty(processor.Calls);
    }

    [Fact]
    public void ExecuteAsync_ShouldRetryThreeTimes_PerSrs()
    {
        var retry = typeof(GenerateThumbnailJob).GetMethod(nameof(GenerateThumbnailJob.ExecuteAsync))!
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), false)
            .Cast<AutomaticRetryAttribute>()
            .Single();

        Assert.Equal(3, retry.Attempts);
    }

    [Fact]
    public void ImageSharpProcessor_ShouldResizeRealImage_KeepingAspectRatio()
    {
        var processor = new ImageSharpProcessor();
        using var image = new Image<Rgba32>(1600, 900);
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);

        var resized = processor.Resize(ms.ToArray(), GenerateThumbnailJob.MediumMaxWidth, GenerateThumbnailJob.MediumMaxHeight);

        using var output = Image.Load(resized);
        Assert.Equal(800, output.Width);
        Assert.Equal(450, output.Height);
    }
}
