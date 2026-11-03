using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Comments.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Comments.Queries.GetRecipeComments;

/// <summary>FR-CMT-001 — GET /api/v1/recipes/{recipeId}/comments: mới nhất trước, có phân trang.</summary>
public sealed record GetRecipeCommentsQuery(Guid RecipeId, int Page = 1, int PageSize = GetRecipeCommentsQuery.DefaultPageSize)
    : IRequest<PagedResult<CommentDto>>
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
}
