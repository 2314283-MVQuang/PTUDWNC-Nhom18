using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Domain.Interfaces;

public interface ICategoryRepository : IRepository<Category>
{
    Task<bool> HasRecipesAsync(Guid categoryId, CancellationToken ct = default);
}