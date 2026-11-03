using CulinaryBlog.Application.Common.Authorization;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Comments.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Comments.Commands.AddComment;

public sealed class AddCommentCommandHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeComment> comments,
    IUnitOfWork unitOfWork,
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser)
    : IRequestHandler<AddCommentCommand, CommentDto>
{
    public async Task<CommentDto> Handle(AddCommentCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new UnauthorizedException("Bạn cần đăng nhập để bình luận.");
        }

        var recipe = await recipes.Query().AsNoTracking()
            .Where(r => r.Id == request.RecipeId)
            .Select(r => new { r.Status, r.AuthorId })
            .FirstOrDefaultAsync(ct);

        if (recipe is null || !RecipeAuthorization.CanRead(recipe.Status, recipe.AuthorId, currentUser))
        {
            throw new RecipeNotFoundException(request.RecipeId);
        }

        if (recipe.Status != RecipeStatus.Published)
        {
            throw new ConflictException("Chỉ bình luận được công thức đã xuất bản.");
        }

        // Token còn hạn (≤ 15 phút) nhưng tài khoản vừa bị Admin khoá thì không cho bình luận.
        var author = await userManager.FindByIdAsync(currentUser.UserId);
        if (author is null || !author.IsActive)
        {
            throw new ForbiddenAccessException("Tài khoản đã bị vô hiệu hoá.");
        }

        var comment = new RecipeComment
        {
            RecipeId = request.RecipeId,
            AuthorId = author.Id,
            Content = request.Content.Trim(),
        };
        await comments.AddAsync(comment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return CommentPermissions.ToDto(comment, author, recipe.AuthorId, currentUser.UserId, currentUser.IsInRole("Admin"));
    }
}
