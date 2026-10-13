using CulinaryBlog.Application.Features.Recipes.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.ArchiveRecipe;

/// <summary>
/// FR-RCP-006 — PATCH /api/v1/recipes/{id}/archive và /unarchive (Actor: Author-Owner / Admin).
/// <paramref name="Archive"/> = true: lưu trữ; false: bỏ lưu trữ.
/// </summary>
public sealed record ArchiveRecipeCommand(
    Guid Id,
    bool Archive = true)
    : IRequest<RecipeDetailDto>;
