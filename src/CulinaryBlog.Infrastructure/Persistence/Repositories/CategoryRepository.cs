    using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

public class CategoryRepository(CulinaryBlogDbContext dbContext)
    : RepositoryBase<Category>(dbContext), ICategoryRepository
{
    public Task<bool> HasRecipesAsync(Guid categoryId, CancellationToken ct = default) =>
        DbContext.Recipes.AnyAsync(recipe => recipe.CategoryId == categoryId, ct);
}