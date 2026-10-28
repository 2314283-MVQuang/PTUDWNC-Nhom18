using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>Kiểm tra kết nối, xác thực và bucket thực tế, không ghi/xóa object.</summary>
public sealed class MinioHealthCheck(IOptions<MinioOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        try
        {
            using var client = new MinioClient().WithEndpoint(settings.Endpoint)
                .WithCredentials(settings.AccessKey, settings.SecretKey)
                .WithSSL(settings.UseSsl).WithTimeout(3000).Build();
            var exists = await client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(settings.BucketName), cancellationToken);
            return exists ? HealthCheckResult.Healthy("MinIO bucket is accessible.")
                : HealthCheckResult.Unhealthy("MinIO bucket is unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            // Phản hồi health không tiết lộ endpoint, credential hoặc lỗi nội bộ.
            return HealthCheckResult.Unhealthy("MinIO is unavailable.");
        }
    }
}
