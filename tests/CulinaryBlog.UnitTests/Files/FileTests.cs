using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Files.Commands.GeneratePresignedUrl;
using CulinaryBlog.Application.Features.Files.Queries.GetUploadedFileById;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.UnitTests.Files;

public class TestFileStorageService : IFileStorageService
{
    public string BucketName => "test-bucket";

    public Task<string> GeneratePresignedUploadUrlAsync(string key, string contentType, int expirySeconds = 900, CancellationToken ct = default)
    {
        return Task.FromResult($"https://minio.test.local/{BucketName}/{key}?X-Amz-Signature=test-sig");
    }

    public string GetPublicUrl(string key)
    {
        return $"https://minio.test.local/{BucketName}/{key}";
    }
}

public class TestCurrentUser : ICurrentUser
{
    public string? UserId => "test-user-id";
    public string? Email => "test@culinaryblog.local";
    public bool IsAuthenticated => true;
    public bool IsInRole(string role) => true;
}

public class FileTests
{
    private CulinaryBlogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CulinaryBlogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CulinaryBlogDbContext(options);
    }

    [Fact]
    public async Task GeneratePresignedUrl_ShouldSucceed_AndSaveToDatabase_WhenValidInput()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new UploadedFileRepository(db);
        var storage = new TestFileStorageService();
        var user = new TestCurrentUser();
        var handler = new GeneratePresignedUrlCommandHandler(storage, repo, user);

        var command = new GeneratePresignedUrlCommand(
            FileName: "pho-bo-ha-noi.jpg",
            ContentType: "image/jpeg",
            Size: 2 * 1024 * 1024 // 2MB
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.FileId);
        Assert.Contains("pho-bo-ha-noi.jpg", result.StorageKey);
        Assert.Contains("test-sig", result.UploadUrl);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(2 * 1024 * 1024, result.Size);

        // Kiểm tra metadata đã lưu vào DB
        var savedInDb = await db.UploadedFiles.FirstOrDefaultAsync(f => f.Id == result.FileId);
        Assert.NotNull(savedInDb);
        Assert.Equal("pho-bo-ha-noi.jpg", savedInDb.FileName);
        Assert.Equal("image/jpeg", savedInDb.ContentType);
        Assert.Equal("test-user-id", savedInDb.UploadedBy);
        Assert.Equal("test-bucket", savedInDb.BucketName);
    }

    [Theory]
    [InlineData("script.exe", "image/jpeg")]
    [InlineData("document.pdf", "image/png")]
    [InlineData("virus.bat", "image/jpeg")]
    [InlineData("hack.sh", "image/png")]
    public async Task GeneratePresignedUrl_ShouldThrowInvalidFileException_WhenExtensionInvalid(string fileName, string contentType)
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new UploadedFileRepository(db);
        var storage = new TestFileStorageService();
        var user = new TestCurrentUser();
        var handler = new GeneratePresignedUrlCommandHandler(storage, repo, user);

        var command = new GeneratePresignedUrlCommand(fileName, contentType, 1024);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidFileException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("không được hỗ trợ", ex.Message);
    }

    [Theory]
    [InlineData("image.jpg", "application/x-msdownload")]
    [InlineData("image.png", "text/html")]
    [InlineData("image.jpg", "application/pdf")]
    [InlineData("image.webp", "video/mp4")]
    public async Task GeneratePresignedUrl_ShouldThrowInvalidFileException_WhenContentTypeInvalid(string fileName, string contentType)
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new UploadedFileRepository(db);
        var storage = new TestFileStorageService();
        var user = new TestCurrentUser();
        var handler = new GeneratePresignedUrlCommandHandler(storage, repo, user);

        var command = new GeneratePresignedUrlCommand(fileName, contentType, 1024);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidFileException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("không hợp lệ", ex.Message);
    }

    [Fact]
    public async Task GeneratePresignedUrl_ShouldThrowInvalidFileException_WhenSizeExceeds10MB()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new UploadedFileRepository(db);
        var storage = new TestFileStorageService();
        var user = new TestCurrentUser();
        var handler = new GeneratePresignedUrlCommandHandler(storage, repo, user);

        var command = new GeneratePresignedUrlCommand(
            "large-image.png",
            "image/png",
            11 * 1024 * 1024 // 11MB > 10MB
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidFileException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("vượt quá giới hạn cho phép tối đa 10 MB", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task GeneratePresignedUrl_ShouldThrowInvalidFileException_WhenSizeZeroOrNegative(long invalidSize)
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new UploadedFileRepository(db);
        var storage = new TestFileStorageService();
        var user = new TestCurrentUser();
        var handler = new GeneratePresignedUrlCommandHandler(storage, repo, user);

        var command = new GeneratePresignedUrlCommand("test.jpg", "image/jpeg", invalidSize);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidFileException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("phải lớn hơn 0", ex.Message);
    }

    [Fact]
    public async Task GetUploadedFileById_ShouldReturnDto_WhenFileExists()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new UploadedFileRepository(db);

        var file = new UploadedFile
        {
            Id = Guid.NewGuid(),
            FileName = "banh-mi.png",
            ContentType = "image/png",
            Size = 512000,
            StorageKey = "uploads/2026/09/banh-mi.png",
            Url = "https://minio.test.local/recipe-images/uploads/2026/09/banh-mi.png",
            BucketName = "recipe-images",
            UploadedBy = "chef-123"
        };
        await db.UploadedFiles.AddAsync(file);
        await db.SaveChangesAsync();

        var handler = new GetUploadedFileByIdQueryHandler(repo);

        // Act
        var result = await handler.Handle(new GetUploadedFileByIdQuery(file.Id), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(file.Id, result.Id);
        Assert.Equal("banh-mi.png", result.FileName);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(512000, result.Size);
        Assert.Equal("chef-123", result.UploadedBy);
    }

    [Fact]
    public async Task GetUploadedFileById_ShouldThrowNotFoundException_WhenFileDoesNotExist()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new UploadedFileRepository(db);
        var handler = new GetUploadedFileByIdQueryHandler(repo);

        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetUploadedFileByIdQuery(nonExistentId), CancellationToken.None));
    }
}
