using CulinaryBlog.Application.Common.Authorization;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Comments.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Comments.Queries.GetRecipeComments;

public sealed class GetRecipeCommentsQueryHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeComment> comments,
    ICurrentUser currentUser)
    : IRequestHandler<GetRecipeCommentsQuery, PagedResult<CommentDto>>
{
    public async Task<PagedResult<CommentDto>> Handle(GetRecipeCommentsQuery request, CancellationToken ct)
    {
        var recipe = await recipes.Query().AsNoTracking()
            .Where(r => r.Id == request.RecipeId)
            .Select(r => new { r.Status, r.AuthorId })
            .FirstOrDefaultAsync(ct);

        // Bản nháp/lưu trữ của người khác: trả 404 như GET chi tiết, không tiết lộ là có tồn tại.
        if (recipe is null || !RecipeAuthorization.CanRead(recipe.Status, recipe.AuthorId, currentUser))
        {
            throw new RecipeNotFoundException(request.RecipeId);
        }

        var query = comments.Query().AsNoTracking().Where(c => c.RecipeId == request.RecipeId);
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(c => c.Author)
            .ToListAsync(ct);

        var isAdmin = currentUser.IsInRole("Admin");
        var items = rows
            .Select(c => CommentPermissions.ToDto(c, c.Author, recipe.AuthorId, currentUser.UserId, isAdmin))
            .ToList();

        return PagedResult<CommentDto>.Create(items, request.Page, request.PageSize, total);
    }
}
