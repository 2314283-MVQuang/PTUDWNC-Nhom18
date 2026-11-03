using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Exceptions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using System.Text.Json;

namespace CulinaryBlog.Infrastructure.Services;

public sealed class MinioRecipeImageStorage : IRecipeImageStorage, IDisposable
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;

    public MinioRecipeImageStorage(IOptions<MinioOptions> options)
    {
        _options = options.Value;
        var builder = new MinioClient().WithEndpoint(_options.Endpoint).WithCredentials(_options.AccessKey, _options.SecretKey);
        if (_options.UseSsl) builder = builder.WithSSL();
        _client = builder.Build();
    }

    public async Task<string> UploadAsync(Guid recipeId, byte[] content, string contentType, string extension, CancellationToken ct)
    {
        var key = $"recipes/{recipeId}/{Guid.NewGuid():N}{extension}";
        using var stream = new MemoryStream(content, writable: false);
        try
        {
            if (!await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_options.BucketName), ct))
            {
                try { await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_options.BucketName), ct); }
                catch { if (!await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_options.BucketName), ct)) throw; }
                var policy = JsonSerializer.Serialize(new { Version = "2012-10-17", Statement = new[] {
                    new { Effect = "Allow", Principal = new { AWS = new[] { "*" } }, Action = new[] { "s3:GetObject" }, Resource = new[] { $"arn:aws:s3:::{_options.BucketName}/recipes/*" } }
                }});
                await _client.SetPolicyAsync(new SetPolicyArgs().WithBucket(_options.BucketName).WithPolicy(policy), ct);
            }
            await _client.PutObjectAsync(new PutObjectArgs().WithBucket(_options.BucketName).WithObject(key)
                .WithStreamData(stream).WithObjectSize(content.Length).WithContentType(contentType), ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new FileStorageUnavailableException(error);
        }
        var origin = string.IsNullOrWhiteSpace(_options.PublicEndpoint)
            ? $"{(_options.UseSsl ? "https" : "http")}://{_options.Endpoint}" : _options.PublicEndpoint.TrimEnd('/');
        return $"{origin}/{_options.BucketName}/{key}";
    }

    public async Task DeleteAsync(Guid recipeId, string publicUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri)) return;
        var origin = string.IsNullOrWhiteSpace(_options.PublicEndpoint)
            ? $"{(_options.UseSsl ? "https" : "http")}://{_options.Endpoint}" : _options.PublicEndpoint.TrimEnd('/');
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var configuredOrigin)
            || Uri.Compare(uri, configuredOrigin, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0) return;
        var prefix = $"/{_options.BucketName}/recipes/{recipeId}/";
        // Chỉ xóa object thuộc chính công thức; không xóa URL ngoài hoặc tệp của công thức khác.
        if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) return;
        var fileName = uri.AbsolutePath[prefix.Length..];
        if (fileName.Contains('/') || fileName.Contains('%') || fileName.Length == 0) return;
        if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(fileName), "N", out _)
            || Path.GetExtension(fileName) is not (".jpg" or ".png" or ".webp" or ".avif")) return;
        var key = $"recipes/{recipeId}/{fileName}";
        await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_options.BucketName).WithObject(key), ct);
    }

    public void Dispose() => _client.Dispose();
}
