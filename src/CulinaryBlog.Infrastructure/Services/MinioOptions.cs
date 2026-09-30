namespace CulinaryBlog.Infrastructure.Services;

/// <summary>
/// Cấu hình kết nối MinIO Object Storage (FR-FILE-001).
/// </summary>
public class MinioOptions
{
    public const string SectionName = "MinIO";

    public string Endpoint { get; set; } = "localhost:9000";

    public string AccessKey { get; set; } = "minioadmin";

    public string SecretKey { get; set; } = "minioadmin";

    public string BucketName { get; set; } = "recipe-images";

    public bool UseSsl { get; set; } = false;

    public string? PublicEndpoint { get; set; }
}
