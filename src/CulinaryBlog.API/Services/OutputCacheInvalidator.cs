using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.API.Services;

/// <summary>
/// Triển khai ICacheInvalidator bằng ASP.NET Core Output Cache (hỗ trợ EvictByTagAsync).
/// Tuân thủ SRS NFR-REL-002: Khi Redis offline hoặc mất kết nối, chỉ log warning,
/// không throw exception làm sập các command thêm/sửa/xóa ảnh, công thức hoặc danh mục.
/// </summary>
public class OutputCacheInvalidator(
    IOutputCacheStore outputCacheStore,
    ILogger<OutputCacheInvalidator> logger) : ICacheInvalidator
{
    public async Task EvictByTagAsync(string tag, CancellationToken ct = default)
    {
        try
        {
            await outputCacheStore.EvictByTagAsync(tag, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[OutputCacheInvalidator] Không thể xóa cache tag '{Tag}' do Redis offline. Tiếp tục theo SRS NFR-REL-002.", tag);
        }
    }
}
