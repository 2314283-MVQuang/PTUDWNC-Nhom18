using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CulinaryBlog.UnitTests.Jobs;

public class MockThumbnailFileStorageService : IFileStorageService
{
    public string BucketName => "culinaryblog";
    public readonly Dictionary<string, byte[]> Files = new();

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
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        Files[key] = ms.ToArray();
        return Task.FromResult($"https://storage.local/{BucketName}/{key}");
    }

    public string GetPublicUrl(string key) => $"https://storage.local/{BucketName}/{key}";
}

public class MockImageProcessor : IImageProcessor
{
    public byte[] Resize(byte[] inputBytes, int maxWidth, int maxHeight)
    {
        // Return dummy bytes representing resized image
        return [.. inputBytes, (byte)(maxWidth % 256), (byte)(maxHeight % 256)];
    }

    public Task<Stream> ResizeAsync(Stream inputStream, int maxWidth, int maxHeight, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        inputStream.CopyTo(ms);
        var bytes = ms.ToArray();
        var resized = Resize(bytes, maxWidth, maxHeight);
        return Task.FromResult<Stream>(new MemoryStream(resized));
    }
}

/// <summary>
/// FR-JOB-002 (Tuần 7 — Tiến): Unit tests cho background job sinh Thumbnail & Medium image.
/// </summary>
public class GenerateThumbnailJobTests
{
    private static (IServiceScopeFactory ScopeFactory, DbContextOptions<CulinaryBlogDbContext> Options) CreateScopeFactory()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<CulinaryBlogDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var services = new ServiceCollection();
        services.AddDbContext<CulinaryBlogDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IServiceScopeFactory>(), options);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateThumbnails_ForRecipeImage()
    {
        // Arrange
        var (scopeFactory, options) = CreateScopeFactory();
        var storage = new MockThumbnailFileStorageService();
        var processor = new MockImageProcessor();

        var sampleBytes = new byte[] { 10, 20, 30, 40 };
        var storageKey = "recipes/food.jpg";
        storage.Files[storageKey] = sampleBytes;

        var recipeImageId = Guid.NewGuid();

        using (var setupDb = new CulinaryBlogDbContext(options))
        {
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                Title = "Phở Bò",
                Slug = "pho-bo",
                Description = "Món phở bò truyền thống thơm ngon",
                AuthorId = "user1",
                CookTime = 30,
                PrepTime = 15,
                Servings = 4,
                CreatedAt = DateTime.UtcNow
            };
            setupDb.Recipes.Add(recipe);

            var recipeImage = new RecipeImage
            {
                Id = recipeImageId,
                RecipeId = recipe.Id,
                OriginalUrl = storage.GetPublicUrl(storageKey),
                OrderIndex = 0
            };
            setupDb.RecipeImages.Add(recipeImage);
            await setupDb.SaveChangesAsync();
        }

        var job = new GenerateThumbnailJob(
            scopeFactory,
            processor,
            storage,
            NullLogger<GenerateThumbnailJob>.Instance);

        // Act
        await job.ExecuteAsync(recipeImageId);

        // Assert
        using (var assertDb = new CulinaryBlogDbContext(options))
        {
            var updated = await assertDb.RecipeImages.FirstOrDefaultAsync(x => x.Id == recipeImageId);
            Assert.NotNull(updated);
            Assert.NotNull(updated.MediumUrl);
            Assert.NotNull(updated.ThumbnailUrl);
            Assert.Contains("_medium", updated.MediumUrl);
            Assert.Contains("_thumb", updated.ThumbnailUrl);
            Assert.Contains(storage.BucketName, updated.MediumUrl);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateThumbnails_ForUploadedFile()
    {
        // Arrange
        var (scopeFactory, options) = CreateScopeFactory();
        var storage = new MockThumbnailFileStorageService();
        var processor = new MockImageProcessor();

        var sampleBytes = new byte[] { 1, 2, 3, 4, 5 };
        var storageKey = "uploads/2026/avatar.png";
        storage.Files[storageKey] = sampleBytes;

        var fileId = Guid.NewGuid();

        using (var setupDb = new CulinaryBlogDbContext(options))
        {
            var uploadedFile = new UploadedFile
            {
                Id = fileId,
                FileName = "avatar.png",
                StorageKey = storageKey,
                Url = storage.GetPublicUrl(storageKey),
                ContentType = "image/png",
                Size = sampleBytes.Length,
                BucketName = storage.BucketName,
                UploadedBy = "user1",
                CreatedAt = DateTime.UtcNow
            };
            setupDb.UploadedFiles.Add(uploadedFile);
            await setupDb.SaveChangesAsync();
        }

        var job = new GenerateThumbnailJob(
            scopeFactory,
            processor,
            storage,
            NullLogger<GenerateThumbnailJob>.Instance);

        // Act
        await job.ExecuteAsync(fileId);

        // Assert
        using (var assertDb = new CulinaryBlogDbContext(options))
        {
            var updated = await assertDb.UploadedFiles.FirstOrDefaultAsync(x => x.Id == fileId);
            Assert.NotNull(updated);
            Assert.NotNull(updated.MediumUrl);
            Assert.NotNull(updated.ThumbnailUrl);
            Assert.Contains("_medium", updated.MediumUrl);
            Assert.Contains("_thumb", updated.ThumbnailUrl);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSkipGracefully_WhenEntityNotFound()
    {
        // Arrange
        var (scopeFactory, _) = CreateScopeFactory();
        var storage = new MockThumbnailFileStorageService();
        var processor = new MockImageProcessor();

        var job = new GenerateThumbnailJob(
            scopeFactory,
            processor,
            storage,
            NullLogger<GenerateThumbnailJob>.Instance);

        // Act & Assert (Không ném exception)
        await job.ExecuteAsync(Guid.NewGuid());
    }

    [Fact]
    public void ImageSharpProcessor_ShouldResizeRealImage()
    {
        // Arrange
        var processor = new ImageSharpProcessor();

        // Tạo 1 ảnh thật 400x300 trong memory
        using var image = new Image<Rgba32>(400, 300);
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        var inputBytes = ms.ToArray();

        // Act
        var resizedBytes = processor.Resize(inputBytes, 150, 100);

        // Assert
        Assert.NotNull(resizedBytes);
        Assert.NotEmpty(resizedBytes);

        // Đọc lại để kiểm tra kích thước mới không vượt quá max
        using var resizedImage = Image.Load(resizedBytes);
        Assert.True(resizedImage.Width <= 150);
        Assert.True(resizedImage.Height <= 100);
    }
}
