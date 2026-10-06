using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cài đặt truy vấn đặc thù cho Recipe.
/// CRUD cơ bản dùng lại RepositoryBase.
/// </summary>
public class RecipeRepository(CulinaryBlogDbContext dbContext)
    : RepositoryBase<Recipe>(dbContext), IRecipeRepository
{
    public Task<Recipe?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken ct = default) =>
        DbSet
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Author)
            .Include(x => x.Steps.OrderBy(x => x.StepNumber))
            .Include(x => x.Ingredients.OrderBy(x => x.OrderIndex))
            .Include(x => x.Images.OrderBy(x => x.OrderIndex))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Recipe?> GetBySlugWithDetailsAsync(
        string slug,
        CancellationToken ct = default) =>
        DbSet
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Author)
            .Include(x => x.Steps.OrderBy(x => x.StepNumber))
            .Include(x => x.Ingredients.OrderBy(x => x.OrderIndex))
            .Include(x => x.Images.OrderBy(x => x.OrderIndex))
            .FirstOrDefaultAsync(x => x.Slug == slug, ct);

    public Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludeRecipeId = null,
        CancellationToken ct = default)
    {
        var query = DbSet
            .IgnoreQueryFilters()
            .Where(x => x.Slug == slug);

        if (excludeRecipeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeRecipeId.Value);
        }

        return query.AnyAsync(ct);
    }

    public void SetOriginalRowVersion(
        Recipe recipe,
        byte[] expectedRowVersion)
    {
        DbContext
            .Entry(recipe)
            .Property(x => x.RowVersion)
            .OriginalValue = expectedRowVersion;
    }
}
