namespace CulinaryBlog.Application.Common.Helpers;

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
}
