using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Comments.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Comments.Commands.DeleteComment;

public sealed class DeleteCommentCommandHandler(
    IRepository<RecipeComment> comments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ILogger<DeleteCommentCommandHandler> logger)
    : IRequestHandler<DeleteCommentCommand>
{
    public async Task Handle(DeleteCommentCommand request, CancellationToken ct)
    {
        var comment = await comments.Query()
            .Include(c => c.Recipe)
            .FirstOrDefaultAsync(c => c.Id == request.CommentId, ct)
            ?? throw new NotFoundException(nameof(RecipeComment), request.CommentId);

        var isAdmin = currentUser.IsInRole("Admin");
        if (!CommentPermissions.CanDelete(comment.AuthorId, comment.Recipe.AuthorId, currentUser.UserId, isAdmin))
        {
            throw new ForbiddenAccessException("Bạn không có quyền xoá bình luận này.");
        }

        comment.IsDeleted = true;
        comment.DeletedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);

        if (!string.Equals(comment.AuthorId, currentUser.UserId, StringComparison.Ordinal))
        {
            logger.LogInformation("Người dùng {UserId} (Admin={IsAdmin}) xoá bình luận {CommentId} của {AuthorId}",
                currentUser.UserId, isAdmin, comment.Id, comment.AuthorId);
        }
    }
}
