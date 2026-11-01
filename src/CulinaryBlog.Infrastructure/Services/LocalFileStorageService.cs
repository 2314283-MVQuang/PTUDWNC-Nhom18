using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>
/// Fallback IFileStorageService lưu file vào thư mục AppContext.BaseDirectory/uploads
/// khi MinIO không chạy (Docker offline).
/// File được phục vụ qua endpoint GET /uploads/{*filePath}.
/// </summary>
public class LocalFileStorageService(ILogger<LocalFileStorageService> logger) : IFileStorageService
{
    public static readonly string UploadsFolder = Path.Combine(AppContext.BaseDirectory, "uploads");

    public string BucketName => "local";

    public Task<string> GeneratePresignedUploadUrlAsync(
        string key, string contentType, int expirySeconds = 900, CancellationToken ct = default)
    {
        logger.LogWarning(
            "[LocalFileStorageService] MinIO offline: Presigned URL trả rỗng. Client chuyển sang direct-upload.");
        return Task.FromResult(string.Empty);
    }

    public async Task<Stream> GetFileStreamAsync(string key, CancellationToken ct = default)
    {
        var cleanKey = CleanKey(key);
        var filePath = Path.Combine(UploadsFolder, cleanKey.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"[LocalFileStorageService] Không tìm thấy file: {key}", filePath);

        var ms = new MemoryStream();
        await using var fs = File.OpenRead(filePath);
        await fs.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task<string> UploadFileAsync(
        string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var cleanKey = CleanKey(key);
        var filePath = Path.Combine(UploadsFolder, cleanKey.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);

        if (content.CanSeek) content.Position = 0;
        await using var fs = File.Create(filePath);
        await content.CopyToAsync(fs, ct);

        var publicUrl = $"/uploads/{cleanKey}";
        logger.LogInformation("[LocalFileStorageService] Đã lưu file: {Path} → {Url}", filePath, publicUrl);
        return publicUrl;
    }

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        var cleanKey = CleanKey(objectName);
        var filePath = Path.Combine(UploadsFolder, cleanKey.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            logger.LogInformation("[LocalFileStorageService] Đã xóa file: {Path}", filePath);
        }
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key)
    {
        var cleanKey = CleanKey(key);
        return $"/uploads/{cleanKey}";
    }

    private static string CleanKey(string key)
    {
        var k = key.Trim().TrimStart('/');
        if (k.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            k = k["uploads/".Length..];
        }
        return k;
    }
}
