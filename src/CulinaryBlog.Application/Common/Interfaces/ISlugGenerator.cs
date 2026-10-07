namespace CulinaryBlog.Application.Common.Interfaces;

public interface ISlugGenerator
{
    Task<string> GenerateUniqueAsync(string value, CancellationToken ct = default);
    Task<string> GenerateUniqueAsync(string value, Guid? excludeId, CancellationToken ct = default);
}