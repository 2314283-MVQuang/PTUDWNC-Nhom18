using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Comments.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Comments.Commands.UpdateComment;

public sealed class UpdateCommentCommandHandler(
    IRepository<RecipeComment> comments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<UpdateCommentCommand, CommentDto>
{
    public async Task<CommentDto> Handle(UpdateCommentCommand request, CancellationToken ct)
    {
        var comment = await comments.Query()
            .Include(c => c.Author)
            .Include(c => c.Recipe)
            .FirstOrDefaultAsync(c => c.Id == request.CommentId, ct)
            ?? throw new NotFoundException(nameof(RecipeComment), request.CommentId);

        if (!CommentPermissions.CanEdit(comment.AuthorId, currentUser.UserId))
        {
            throw new ForbiddenAccessException("Bạn chỉ sửa được bình luận của chính mình.");
        }

        var content = request.Content.Trim();
        if (!string.Equals(comment.Content, content, StringComparison.Ordinal))
        {
            comment.Content = content;
            comment.IsEdited = true;
            await unitOfWork.SaveChangesAsync(ct);
        }

        return CommentPermissions.ToDto(comment, comment.Author, comment.Recipe.AuthorId,
            currentUser.UserId, currentUser.IsInRole("Admin"));
    }
}
