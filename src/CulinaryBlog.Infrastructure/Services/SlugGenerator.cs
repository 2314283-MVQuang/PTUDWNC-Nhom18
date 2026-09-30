using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Services;

public class SlugGenerator(CulinaryBlogDbContext dbContext) : ISlugGenerator
{
    public async Task<string> GenerateUniqueAsync(
        string value,
        CancellationToken ct = default,
        Guid? categoryIdToExclude = null)
    {
        var baseSlug = SlugHelper.GenerateSlug(value);
        var candidate = baseSlug;
        var suffix = 2;

        while (await SlugExistsAsync(candidate, categoryIdToExclude, ct))
        {
            candidate = SlugHelper.AppendSuffix(baseSlug, suffix++);
        }

        return candidate;
    }

    private async Task<bool> SlugExistsAsync(
        string slug,
        Guid? categoryIdToExclude,
        CancellationToken ct)
    {
        var categories = dbContext.Categories.IgnoreQueryFilters().Where(x => x.Slug == slug);
        if (categoryIdToExclude is Guid id)
        {
            categories = categories.Where(x => x.Id != id);
        }

        return await categories.AnyAsync(ct)
            || await dbContext.Recipes.IgnoreQueryFilters().AnyAsync(x => x.Slug == slug, ct);
    }
}