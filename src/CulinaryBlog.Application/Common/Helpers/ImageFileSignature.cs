namespace CulinaryBlog.Application.Common.Helpers;

/// <summary>
/// FR-RCP-008 bước 4 (Tuần 8 — Thịnh): xác định loại ảnh THẬT bằng magic bytes ở đầu file, không
/// tin Content-Type/đuôi file do trình duyệt gửi. File đổi đuôi .exe → .png sẽ bị nhận ra.
/// </summary>
public static class ImageFileSignature
{
    /// <summary>Số byte cần đọc ở đầu file để nhận dạng.</summary>
    public const int HeaderLength = 12;

    /// <summary>Trả content type (image/jpeg, image/png, image/webp, image/avif) hoặc null nếu không phải ảnh hợp lệ.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        // RIFF....WEBP
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        // ISO BMFF: ....ftypavif / ftypavis
        if (header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8)
            && (header[8..12].SequenceEqual("avif"u8) || header[8..12].SequenceEqual("avis"u8)))
        {
            return "image/avif";
        }

        return null;
    }
}
