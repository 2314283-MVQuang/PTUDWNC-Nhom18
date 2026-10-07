using CulinaryBlog.Application.Features.Categories.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategories;

/// <summary>FR-CAT-001 — GET /api/v1/categories (public, không cần đăng nhập).</summary>
public record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;