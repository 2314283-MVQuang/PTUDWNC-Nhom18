using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace CulinaryBlog.Infrastructure.Services;

internal sealed class MinioBucketInitializer(
    IMinioClient minioClient,
    IOptions<MinioOptions> options,
    ILogger<MinioBucketInitializer> logger) : IHostedService
{
    private const int MaxAttempts = 12;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bucketName = options.Value.BucketName;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var exists = await minioClient.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(bucketName),
                    cancellationToken);

                if (!exists)
                {
                    await minioClient.MakeBucketAsync(
                        new MakeBucketArgs().WithBucket(bucketName),
                        cancellationToken);

                    logger.LogInformation("Đã tạo bucket MinIO '{BucketName}'.", bucketName);
                }

                await minioClient.SetPolicyAsync(
                    new SetPolicyArgs()
                        .WithBucket(bucketName)
                        .WithPolicy(CreatePublicReadPolicy(bucketName)),
                    cancellationToken);

                logger.LogInformation(
                    "Bucket MinIO '{BucketName}' sẵn sàng; chỉ cho phép đọc object công khai.",
                    bucketName);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Không thể khởi tạo bucket MinIO '{BucketName}' (lần {Attempt}/{MaxAttempts}).",
                    bucketName,
                    attempt,
                    MaxAttempts);

                if (attempt == MaxAttempts)
                {
                    throw new InvalidOperationException(
                        $"Không thể khởi tạo bucket MinIO '{bucketName}' sau {MaxAttempts} lần thử.",
                        exception);
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt, 5)), cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
