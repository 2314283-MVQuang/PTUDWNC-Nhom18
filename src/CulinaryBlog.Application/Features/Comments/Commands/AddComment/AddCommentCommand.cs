using CulinaryBlog.Application.Features.Comments.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Features.Comments.Commands.AddComment;

/// <summary>FR-CMT-002 — POST /api/v1/recipes/{recipeId}/comments { content }.</summary>
public sealed record AddCommentCommand(Guid RecipeId, string Content) : IRequest<CommentDto>;
