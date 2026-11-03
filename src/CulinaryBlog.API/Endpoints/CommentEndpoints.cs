using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Comments.Commands.AddComment;
using CulinaryBlog.Application.Features.Comments.Commands.DeleteComment;
using CulinaryBlog.Application.Features.Comments.Commands.UpdateComment;
using CulinaryBlog.Application.Features.Comments.Dtos;
using CulinaryBlog.Application.Features.Comments.Queries.GetRecipeComments;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

public sealed record CommentRequest(string Content);

/// <summary>
/// Tuần 9 (Quang) — FR-CMT (bổ sung SRS v1.1.0): bình luận dưới công thức.
///   GET    /api/v1/recipes/{recipeId}/comments   — ai cũng xem (công thức Published)
///   POST   /api/v1/recipes/{recipeId}/comments   — người đã đăng nhập, 5 bình luận/phút
///   PUT    /api/v1/comments/{commentId}          — chỉ người viết
///   DELETE /api/v1/comments/{commentId}          — người viết, tác giả công thức hoặc Admin
/// Không cache: danh sách đổi liên tục và có cờ CanEdit/CanDelete riêng theo người xem.
/// </summary>
public static class CommentEndpoints
{
    public static void MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var recipeComments = app.MapV1Group("/recipes/{recipeId:guid}/comments").WithTags("Comments");

        recipeComments.MapGet("", async (Guid recipeId, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRecipeCommentsQuery(
                recipeId, page ?? 1, pageSize ?? GetRecipeCommentsQuery.DefaultPageSize), ct);
            return result.ToPagedResponse();
        })
        .AllowAnonymous()
        .WithName("GetRecipeComments")
        .WithSummary("FR-CMT-001: Danh sách bình luận của công thức (mới nhất trước)")
        .Produces<PagedApiEnvelope<CommentDto>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        recipeComments.MapPost("", async (Guid recipeId, CommentRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AddCommentCommand(recipeId, request.Content ?? string.Empty), ct);
            return result.ToCreatedResponse($"/api/v1/recipes/{recipeId}/comments/{result.Id}");
        })
        .RequireAuthorization(AuthorizationPolicies.Author)
        .RequireRateLimiting(ApiSecurityExtensions.CommentRateLimit)
        .WithName("AddComment")
        .WithSummary("FR-CMT-002: Viết bình luận (công thức đã xuất bản)")
        .Produces<ApiEnvelope<CommentDto>>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        var comments = app.MapV1Group("/comments").WithTags("Comments")
            .RequireAuthorization(AuthorizationPolicies.Author);

        comments.MapPut("/{commentId:guid}", async (Guid commentId, CommentRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateCommentCommand(commentId, request.Content ?? string.Empty), ct);
            return result.ToOkResponse();
        })
        .WithName("UpdateComment")
        .WithSummary("FR-CMT-003: Sửa bình luận của chính mình")
        .Produces<ApiEnvelope<CommentDto>>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        comments.MapDelete("/{commentId:guid}", async (Guid commentId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteCommentCommand(commentId), ct);
            return Results.NoContent();
        })
        .WithName("DeleteComment")
        .WithSummary("FR-CMT-004: Xoá bình luận (người viết, tác giả công thức hoặc Admin)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
