using MediatR;

namespace CulinaryBlog.Application.Features.Comments.Commands.DeleteComment;

/// <summary>FR-CMT-004 — DELETE /api/v1/comments/{commentId}: người viết, tác giả công thức hoặc Admin (xoá mềm).</summary>
public sealed record DeleteCommentCommand(Guid CommentId) : IRequest;
