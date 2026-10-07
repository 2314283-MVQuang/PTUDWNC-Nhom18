using CulinaryBlog.Application.Features.Categories.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategories;

/// <summary>
/// Dùng thẳng IRepository&lt;Category&gt; (generic, có sẵn cho mọi entity) thay vì tạo riêng
/// ICategoryRepository — đúng tinh thần thiết kế của IRepository&lt;T&gt;.Query() (cho phép Query
/// Handler tự build truy vấn LINQ mà không cần thêm method mới vào repository).
/// RecipeCount chỉ đếm công thức đã Published — Draft/Archived không nên hiện số lượng ra
/// trang công khai.
/// </summary>
public class GetCategoriesQueryHandler(IRepository<Category> categoryRepository)
    : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<IReadOnlyList<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        return await categoryRepository.Query()
            .OrderBy(c => c.OrderIndex)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.ImageUrl,
                c.Recipes.Count(r => r.Status == RecipeStatus.Published)))
            .ToListAsync(ct);
    }
}