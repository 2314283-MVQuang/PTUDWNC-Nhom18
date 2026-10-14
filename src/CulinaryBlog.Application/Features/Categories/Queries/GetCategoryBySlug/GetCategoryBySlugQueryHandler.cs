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
            .Include(x => x.Category)
            .Include(x => x.Author)
            .Include(x => x.Images)
            .Where(x => x.CategoryId == category.Id)
            .Where(x =>
                x.Status == RecipeStatus.Published ||
                (x.Status == RecipeStatus.Draft &&
                 userId != null &&
                 x.AuthorId == userId));

        var totalCount = await recipes.CountAsync(ct);

        // Mới xuất bản lên trước; bản nháp (chưa có PublishedAt) xếp cuối, theo ngày tạo. Ghi rõ
        // "PublishedAt == null" lên đầu vì PostgreSQL mặc định xếp NULL lên TRƯỚC khi sắp giảm dần.
        var entities =
            await recipes
                .OrderBy(x => x.PublishedAt == null)
                .ThenByDescending(x => x.PublishedAt)
                .ThenByDescending(x => x.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

        var items = entities
            .Select(x => x.ToSummaryDto())
            .ToList();

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
