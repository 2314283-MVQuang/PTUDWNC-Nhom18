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
    private readonly IMinioClient _presignedUrlClient;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioFileStorageService> _logger;

    public MinioFileStorageService(
        IMinioClient minioClient,
        IOptions<MinioOptions> options,
        ILogger<MinioFileStorageService> logger)
    {
        _minioClient = minioClient;
        _options = options.Value;
        _logger = logger;
        _presignedUrlClient = CreatePresignedUrlClient(_options);
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

            var presignedUrl = await _presignedUrlClient.PresignedPutObjectAsync(args);
            return presignedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo Presigned PUT URL từ MinIO cho bucket '{Bucket}', key '{Key}'", _options.BucketName, key);
            throw;
        }
    }

    public async Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Tên object cần xóa không được để trống.", nameof(objectName));
        }

        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(args, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi khi xóa object '{ObjectName}' khỏi bucket MinIO '{Bucket}'",
                objectName,
                _options.BucketName);
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

    private static IMinioClient CreatePresignedUrlClient(MinioOptions options)
    {
        var endpoint = options.Endpoint;
        var useSsl = options.UseSsl;

        if (!string.IsNullOrWhiteSpace(options.PublicEndpoint))
        {
            if (!Uri.TryCreate(options.PublicEndpoint, UriKind.Absolute, out var publicUri)
                || publicUri.Scheme is not ("http" or "https"))
            {
                throw new InvalidOperationException(
                    "MinIO:PublicEndpoint phải là một URL HTTP(S) hợp lệ.");
            }

            endpoint = publicUri.Authority;
            useSsl = publicUri.Scheme == Uri.UriSchemeHttps;
        }

        var client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey);

        if (useSsl)
        {
            client = client.WithSSL();
        }

        return client.Build();
    }
}
