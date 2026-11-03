namespace CulinaryBlog.Application.Common.Exceptions;

public sealed class InvalidSortException : Exception
{
    public InvalidSortException(string field, IEnumerable<string> allowedFields)
        : base(
            $"Trường sort '{field}' không hợp lệ. Các trường được hỗ trợ: " +
            $"{string.Join(", ", allowedFields)}.")
    {
    }
}
