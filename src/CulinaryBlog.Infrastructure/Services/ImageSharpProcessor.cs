using CulinaryBlog.Application.Common.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace CulinaryBlog.Infrastructure.Services;

/// <summary>
/// Triển khai IImageProcessor sử dụng SixLabors.ImageSharp (FR-JOB-002: sinh bản medium và thumbnail).
/// </summary>
public class ImageSharpProcessor : IImageProcessor
{
    public byte[] Resize(byte[] inputBytes, int maxWidth, int maxHeight)
    {
        using var image = Image.Load(inputBytes);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(maxWidth, maxHeight),
            Mode = ResizeMode.Max
        }));

        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms, new JpegEncoder { Quality = 85 });
        return ms.ToArray();
    }

    public async Task<Stream> ResizeAsync(Stream inputStream, int maxWidth, int maxHeight, CancellationToken ct = default)
    {
        if (inputStream.CanSeek && inputStream.Position != 0)
        {
            inputStream.Position = 0;
        }

        using var image = await Image.LoadAsync(inputStream, ct);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(maxWidth, maxHeight),
            Mode = ResizeMode.Max
        }));

        var outputStream = new MemoryStream();
        await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 85 }, ct);
        outputStream.Position = 0;
        return outputStream;
    }
}
