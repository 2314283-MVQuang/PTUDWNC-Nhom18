namespace CulinaryBlog.Application.Features.Files.Dtos;

/// <summary>
/// Kết quả trả về khi cấp Presigned URL để upload ảnh lên MinIO.
/// </summary>
public record PresignedUploadUrlDto(
    Guid FileId,
    string UploadUrl,
    string PublicUrl,
    string StorageKey,
    string FileName,
    string ContentType,
    long Size,
    DateTimeOffset ExpiresAt
);
