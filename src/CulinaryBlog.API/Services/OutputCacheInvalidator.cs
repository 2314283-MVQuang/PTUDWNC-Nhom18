using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.AspNetCore.OutputCaching;

namespace CulinaryBlog.API.Services;

/// <summary>
/// Triển khai ICacheInvalidator bằng ASP.NET Core Output Cache (hỗ trợ EvictByTagAsync).
/// </summary>
public class OutputCacheInvalidator(IOutputCacheStore outputCacheStore) : ICacheInvalidator
{
    public async Task EvictByTagAsync(string tag, CancellationToken ct = default)
    {
        await outputCacheStore.EvictByTagAsync(tag, ct);
    }
}
