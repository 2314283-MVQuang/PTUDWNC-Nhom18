namespace CulinaryBlog.Application.Features.Files.Dtos;

/// <summary>
/// DTO chứa metadata của tệp tin đã lưu trữ trong hệ thống.
/// </summary>
public record UploadedFileDto(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string StorageKey,
    string Url,
    string BucketName,
    string? UploadedBy,
    DateTimeOffset CreatedAt
);
