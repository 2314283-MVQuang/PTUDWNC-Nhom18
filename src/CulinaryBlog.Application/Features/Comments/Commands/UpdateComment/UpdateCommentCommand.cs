using CulinaryBlog.Application.Features.Comments.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Comments.Commands.UpdateComment;

/// <summary>FR-CMT-003 — PUT /api/v1/comments/{commentId} { content }: chỉ người viết bình luận.</summary>
public sealed record UpdateCommentCommand(Guid CommentId, string Content) : IRequest<CommentDto>;
