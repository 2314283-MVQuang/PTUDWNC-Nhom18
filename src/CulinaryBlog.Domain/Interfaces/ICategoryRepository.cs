using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Domain.Interfaces;

public interface ICategoryRepository : IRepository<Category>
{
    Task<bool> HasRecipesAsync(Guid categoryId, CancellationToken ct = default);

    /// <summary>FR-CAT-002: tìm danh mục chưa xoá theo slug (slug là duy nhất), không tracking.</summary>
    Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default);
}