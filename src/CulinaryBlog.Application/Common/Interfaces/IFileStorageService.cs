namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ quản lý tệp tin và sinh Presigned URL với MinIO Object Storage (FR-FILE-001).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Sinh URL có chữ ký số (Presigned URL) dùng phương thức PUT để client trực tiếp upload file lên MinIO.
    /// </summary>
    Task<string> GeneratePresignedUploadUrlAsync(string key, string contentType, int expirySeconds = 900, CancellationToken ct = default);

    /// <summary>
    /// Tải dữ liệu tệp tin từ kho lưu trữ MinIO theo storage key.
    /// </summary>
    Task<Stream> GetFileStreamAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Tải tệp tin trực tiếp lên kho lưu trữ MinIO theo storage key.
    /// </summary>
    Task<string> UploadFileAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Lấy đường dẫn truy cập công khai của tệp tin.
    /// </summary>
    string GetPublicUrl(string key);

    /// <summary>
    /// Tên bucket hiện tại đang được cấu hình.
    /// </summary>
    string BucketName { get; }
}
