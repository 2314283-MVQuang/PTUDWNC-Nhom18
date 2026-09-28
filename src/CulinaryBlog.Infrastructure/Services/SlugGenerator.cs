using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Services;

public class SlugGenerator(CulinaryBlogDbContext dbContext) : ISlugGenerator
{
    public Task<string> GenerateUniqueAsync(string value, CancellationToken ct = default) =>
        GenerateUniqueAsync(value, null, ct);

    public async Task<string> GenerateUniqueAsync(string value, Guid? excludeId, CancellationToken ct = default)
    {
        var baseSlug = SlugHelper.GenerateSlug(value);
        var candidate = baseSlug;
        var suffix = 2;

        while (await SlugExistsAsync(candidate, excludeId, ct))
        {
            candidate = SlugHelper.AppendSuffix(baseSlug, suffix++);
        }

        return candidate;
    }

    private async Task<bool> SlugExistsAsync(string slug, Guid? excludeId, CancellationToken ct) =>
        await dbContext.Categories.IgnoreQueryFilters().AnyAsync(x => x.Slug == slug && (excludeId == null || x.Id != excludeId), ct)
        || await dbContext.Recipes.IgnoreQueryFilters().AnyAsync(x => x.Slug == slug && (excludeId == null || x.Id != excludeId), ct);
}