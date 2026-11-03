using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Categories.Dtos;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;

/// <summary>
/// FR-CAT-002. Luồng (theo SRS): tìm danh mục theo slug (404 CATEGORY_NOT_FOUND nếu không có) →
/// lấy công thức thuộc danh mục: Published, cộng thêm bản nháp (Draft) của chính người đang đăng
/// nhập → phân trang OFFSET (Skip (page-1)*pageSize, Take pageSize) → trả CategoryDetailDto.
///
/// Công thức Archived (FR-RCP-006) và đã xoá mềm không bao giờ xuất hiện ở đây. RecipeCount của
/// danh mục chỉ đếm Published, giống GET /categories, để 2 trang hiện cùng một con số.
/// </summary>
public sealed class GetCategoryBySlugQueryHandler(
    ICategoryRepository categoryRepository,
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetCategoryBySlugQuery, CategoryDetailDto>
{
    public async Task<CategoryDetailDto> Handle(
        GetCategoryBySlugQuery request,
        CancellationToken ct)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();

        var category =
            await categoryRepository.GetBySlugAsync(slug, ct)
            ?? throw new CategoryNotFoundException(request.Slug);

        var publishedCount =
            await recipeRepository.Query()
                .CountAsync(
                    x => x.CategoryId == category.Id &&
                         x.Status == RecipeStatus.Published,
                    ct);

        var userId = currentUser.UserId;

        IQueryable<Recipe> recipes = recipeRepository
            .Query()
            .AsNoTracking()
            .Where(x => x.CategoryId == category.Id);

        // Chương 3 (3.6, đo bằng db/explain-analyze.sql): khách xem (đại đa số request) chỉ thấy
        // Published nên điều kiện + thứ tự viết đơn giản để PostgreSQL dùng thẳng index
        // IX_Recipes_CategoryId_Status_PublishedAt (CategoryId, Status, PublishedAt DESC) — đọc đúng
        // 12 dòng thay vì quét theo PublishedAt rồi lọc. Biểu thức "PublishedAt IS NULL" trong
        // ORDER BY không dùng được index nên chỉ giữ cho người đăng nhập (có bản nháp của mình).
        IOrderedQueryable<Recipe> ordered;
        if (userId is null)
        {
            recipes = recipes.Where(x => x.Status == RecipeStatus.Published);
            ordered = recipes.OrderByDescending(x => x.PublishedAt);
        }
        else
        {
            recipes = recipes.Where(x =>
                x.Status == RecipeStatus.Published ||
                (x.Status == RecipeStatus.Draft && x.AuthorId == userId));
            // Bản nháp (chưa có PublishedAt) xếp cuối — PostgreSQL mặc định để NULL lên TRƯỚC khi sắp giảm dần.
            ordered = recipes
                .OrderBy(x => x.PublishedAt == null)
                .ThenByDescending(x => x.PublishedAt);
        }

        var totalCount = await recipes.CountAsync(ct);

        // Chương 3 (3.5.3 — AsNoTracking + Projection): chỉ SELECT các cột của RecipeSummaryDto
        // (tên danh mục, tên tác giả, 1 URL ảnh đầu tiên qua subquery) thay vì Include rồi nạp cả
        // entity Recipe + Author + MỌI ảnh vào bộ nhớ. ThenBy(Id) giữ thứ tự ổn định giữa các trang.
        var items =
            await ordered
                .ThenByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(RecipeDtoMapping.SummaryProjection)
                .ToListAsync(ct);

        return new CategoryDetailDto(
            new CategoryDto(
                category.Id,
                category.Name,
                category.Slug,
                category.Description,
                category.ImageUrl,
                publishedCount),
            PagedResult<RecipeSummaryDto>.Create(
                items,
                request.Page,
                request.PageSize,
                totalCount));
    }
}
