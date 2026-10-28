namespace CulinaryBlog.Application.Common.Helpers;

/// <summary>
/// FR-FILE-002: SRS mô tả DeleteAsync nhận URL rồi "trích xuất object name từ URL". Ảnh công thức
/// lưu URL công khai (http://host:9000/{bucket}/{key}) nên phải bóc ra {key} trước khi xoá.
/// URL không thuộc bucket của hệ thống (ảnh ngoài, URL sai) trả null — không bao giờ xoá nhầm.
/// </summary>
public static class FileStorageObjectNameResolver
{
    public static string? FromPublicUrl(string? publicUrl, string bucketName)
    {
        if (string.IsNullOrWhiteSpace(publicUrl)
            || string.IsNullOrWhiteSpace(bucketName)
            || !Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        var bucketIndex = Array.FindIndex(
            segments,
            segment => string.Equals(segment, bucketName, StringComparison.Ordinal));

        if (bucketIndex < 0 || bucketIndex == segments.Length - 1)
        {
            return null;
        }

        return string.Join('/', segments.Skip(bucketIndex + 1));
    }

    /// <summary>Object name của cả 3 bản ảnh (gốc, medium, thumbnail), bỏ trùng và bỏ URL ngoài bucket.</summary>
    public static IReadOnlyList<string> FromImageUrls(
        string bucketName,
        params string?[] publicUrls) =>
        publicUrls
            .Select(url => FromPublicUrl(url, bucketName))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
