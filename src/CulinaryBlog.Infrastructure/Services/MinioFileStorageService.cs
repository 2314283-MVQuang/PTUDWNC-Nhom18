using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>
/// Dịch vụ lưu trữ tệp tin và sinh Presigned URL với MinIO SDK .NET (FR-FILE-001).
/// </summary>
public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioFileStorageService> _logger;

    public MinioFileStorageService(IOptions<MinioOptions> options, ILogger<MinioFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var client = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey);

        if (_options.UseSsl)
        {
            client = client.WithSSL();
        }

        _minioClient = client.Build();
    }

    public string BucketName => _options.BucketName;

    public async Task<string> GeneratePresignedUploadUrlAsync(
        string key,
        string contentType,
        int expirySeconds = 900,
        CancellationToken ct = default)
    {
        try
        {
            var args = new PresignedPutObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(key)
                .WithExpiry(expirySeconds);

            var presignedUrl = await _minioClient.PresignedPutObjectAsync(args);
            return presignedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo Presigned PUT URL từ MinIO cho bucket '{Bucket}', key '{Key}'", _options.BucketName, key);
            throw;
        }
    }

    public string GetPublicUrl(string key)
    {
        var baseEndpoint = !string.IsNullOrWhiteSpace(_options.PublicEndpoint)
            ? _options.PublicEndpoint.TrimEnd('/')
            : (_options.UseSsl ? $"https://{_options.Endpoint}" : $"http://{_options.Endpoint}");

        return $"{baseEndpoint}/{_options.BucketName}/{key}";
    }
}
