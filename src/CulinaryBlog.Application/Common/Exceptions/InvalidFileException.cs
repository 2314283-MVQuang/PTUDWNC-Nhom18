namespace CulinaryBlog.Application.Common.Exceptions;

/// <summary>
/// Ném ra khi tệp tin upload không hợp lệ về loại định dạng (MIME/extension) hoặc vượt quá kích thước cho phép.
/// Được GlobalExceptionMiddleware xử lý để trả về lỗi RFC 7807 Problem Details (HTTP 400).
/// </summary>
public class InvalidFileException : Exception
{
    public InvalidFileException(string message) : base(message)
    {
    }

    public InvalidFileException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
