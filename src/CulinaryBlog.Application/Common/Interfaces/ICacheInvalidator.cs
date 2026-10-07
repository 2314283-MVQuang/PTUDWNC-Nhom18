namespace CulinaryBlog.Application.Common.Interfaces;

public interface ICacheInvalidator
{
    Task EvictByTagAsync(string tag, CancellationToken ct = default);
}
