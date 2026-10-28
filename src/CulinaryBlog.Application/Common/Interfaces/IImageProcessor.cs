namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ xử lý và biến đổi hình ảnh (FR-JOB-002: sinh bản medium và thumbnail).
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Thay đổi kích thước ảnh từ dữ liệu nhị phân (bytes), giữ đúng tỷ lệ khung hình.
    /// </summary>
    byte[] Resize(byte[] inputBytes, int maxWidth, int maxHeight);

    /// <summary>
    /// Thay đổi kích thước ảnh từ stream, giữ đúng tỷ lệ khung hình.
    /// </summary>
    Task<Stream> ResizeAsync(Stream inputStream, int maxWidth, int maxHeight, CancellationToken ct = default);
}
