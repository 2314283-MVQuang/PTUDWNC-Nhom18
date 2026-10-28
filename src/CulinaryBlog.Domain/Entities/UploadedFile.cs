using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// Lưu trữ thông tin metadata của tệp tin được cấp presigned URL và upload lên MinIO (FR-FILE-001, FR-JOB-002).
/// </summary>
public class UploadedFile : BaseEntity
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>Bản medium (~800px) sinh bởi background job FR-JOB-002.</summary>
    public string? MediumUrl { get; set; }

    /// <summary>Bản thumbnail (~300px) sinh bởi background job FR-JOB-002.</summary>
    public string? ThumbnailUrl { get; set; }

    public string BucketName { get; set; } = string.Empty;

    public string? UploadedBy { get; set; }
}
