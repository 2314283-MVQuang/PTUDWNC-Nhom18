using CulinaryBlog.Domain.Entities;
using MediatR;

namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;

/// <summary>FR-CAT-003 — POST /api/v1/categories (Actor: Admin).</summary>
public record CreateCategoryCommand(
    string Name,
    string? Description = null,
    string? ImageUrl = null) : IRequest<Category>;