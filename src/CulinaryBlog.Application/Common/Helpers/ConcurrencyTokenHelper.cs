using CulinaryBlog.Application.Common.Exceptions;

namespace CulinaryBlog.Application.Common.Helpers;

/// <summary>
/// Chuyển RowVersion từ HTTP If-Match (Base64) thành byte[] để EF Core
/// dùng làm OriginalValue cho optimistic concurrency.
/// Hỗ trợ cả ETag dạng AABB... và "AABB...".
/// </summary>
public static class ConcurrencyTokenHelper
{
    public static byte[] DecodeIfMatch(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new ValidationException(
                new Dictionary<string, string[]>
                {
                    ["If-Match"] = ["Header If-Match là bắt buộc khi cập nhật Recipe."]
                });
        }

        var value = ifMatch.Trim();

        // Strong ETag: "base64"
        if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
        {
            value = value[1..^1];
        }

        // Weak ETag: W/"base64"
        if (value.StartsWith("W/\"", StringComparison.Ordinal) && value.EndsWith('"'))
        {
            value = value[3..^1];
        }

        // Không cho phép wildcard vì bài toán này yêu cầu đúng RowVersion hiện tại.
        if (value == "*")
        {
            throw new ValidationException(
                new Dictionary<string, string[]>
                {
                    ["If-Match"] = ["Không hỗ trợ If-Match: * cho Recipe Update."]
                });
        }

        byte[] result;
        try
        {
            result = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw new ValidationException(
                new Dictionary<string, string[]>
                {
                    ["If-Match"] = ["If-Match phải là RowVersion dạng Base64 hợp lệ."]
                });
        }

        // touch_row() trong PostgreSQL dùng gen_random_bytes(8).
        if (result.Length != 8)
        {
            throw new ValidationException(
                new Dictionary<string, string[]>
                {
                    ["If-Match"] = ["RowVersion của Recipe phải có đúng 8 byte."]
                });
        }

        return result;
    }
}
