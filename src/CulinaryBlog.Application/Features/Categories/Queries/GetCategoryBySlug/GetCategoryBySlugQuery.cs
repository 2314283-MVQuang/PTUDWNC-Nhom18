using CulinaryBlog.Application.Features.Categories.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;

/// <summary>
/// FR-CAT-002 — GET /api/v1/categories/{slug}?page={n}&amp;pageSize={n} (public, không cần đăng nhập).
/// </summary>
public sealed record GetCategoryBySlugQuery(
    string Slug,
    int Page = 1,
    int PageSize = 12)
    : IRequest<CategoryDetailDto>;
