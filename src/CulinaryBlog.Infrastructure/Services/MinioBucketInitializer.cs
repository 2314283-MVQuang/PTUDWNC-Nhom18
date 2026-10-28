using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>
/// Tạo bucket ảnh (public-read) khi API khởi động. Chạy NỀN (BackgroundService) và chỉ ghi log khi
/// MinIO không phản hồi — không chặn/làm sập API: các chức năng không dùng ảnh vẫn chạy được, và
/// health check MinIO (/health/ready) báo trạng thái thật.
/// </summary>
internal sealed class MinioBucketInitializer(
    IMinioClient minioClient,
    IOptions<MinioOptions> options,
    ILogger<MinioBucketInitializer> logger) : BackgroundService
{
    private const int MaxAttempts = 12;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bucketName = options.Value.BucketName;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var exists = await minioClient.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(bucketName),
                    stoppingToken);

                if (!exists)
                {
                    await minioClient.MakeBucketAsync(
                        new MakeBucketArgs().WithBucket(bucketName),
                        stoppingToken);

                    logger.LogInformation("Đã tạo bucket MinIO '{BucketName}'.", bucketName);
                }

                await minioClient.SetPolicyAsync(
                    new SetPolicyArgs()
                        .WithBucket(bucketName)
                        .WithPolicy(CreatePublicReadPolicy(bucketName)),
                    stoppingToken);

                logger.LogInformation(
                    "Bucket MinIO '{BucketName}' sẵn sàng; chỉ cho phép đọc object công khai.",
                    bucketName);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                if (attempt == MaxAttempts)
                {
                    logger.LogError(
                        exception,
                        "Không thể khởi tạo bucket MinIO '{BucketName}' sau {MaxAttempts} lần thử — upload/xoá ảnh sẽ lỗi cho tới khi MinIO chạy.",
                        bucketName,
                        MaxAttempts);
                    return;
                }

                logger.LogWarning(
                    exception,
                    "Không thể khởi tạo bucket MinIO '{BucketName}' (lần {Attempt}/{MaxAttempts}).",
                    bucketName,
                    attempt,
                    MaxAttempts);

                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt, 5)), stoppingToken);
            }
        }
    }

    private static string CreatePublicReadPolicy(string bucketName) =>
        JsonSerializer.Serialize(new
        {
            Version = "2012-10-17",
            Statement = new[]
            {
                new
                {
                    Effect = "Allow",
                    Principal = new { AWS = new[] { "*" } },
                    Action = new[] { "s3:GetObject" },
                    Resource = new[] { $"arn:aws:s3:::{bucketName}/*" }
                }
            }
        });
}
