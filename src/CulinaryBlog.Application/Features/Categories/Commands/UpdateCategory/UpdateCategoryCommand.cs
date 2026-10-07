using CulinaryBlog.Domain.Entities;
using MediatR;

namespace CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;

/// <summary>FR-CAT-004 — PUT /api/v1/categories/{id} (Actor: Admin).</summary>
public record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description = null,
    string? ImageUrl = null) : IRequest<Category>;
