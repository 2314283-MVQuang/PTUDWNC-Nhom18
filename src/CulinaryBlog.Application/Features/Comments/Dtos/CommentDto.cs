using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Features.Comments.Dtos;

public sealed record CommentAuthorDto(string Id, string DisplayName, string? AvatarUrl);

/// <summary>
/// FR-CMT — 1 bình luận. <see cref="CanEdit"/>/<see cref="CanDelete"/> tính theo người đang gọi API
/// để frontend chỉ hiện nút Sửa/Xoá khi được phép (backend vẫn kiểm tra lại khi sửa/xoá).
/// </summary>
public sealed record CommentDto(
    Guid Id,
    Guid RecipeId,
    string Content,
    CommentAuthorDto Author,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsEdited,
    bool CanEdit,
    bool CanDelete);

internal static class CommentPermissions
{
    /// <summary>Sửa: chỉ người viết bình luận (Admin cũng không sửa lời của người khác).</summary>
    public static bool CanEdit(string commentAuthorId, string? currentUserId) =>
        currentUserId is not null && string.Equals(commentAuthorId, currentUserId, StringComparison.Ordinal);

    /// <summary>Xoá: người viết bình luận, tác giả công thức (quản lý bài của mình) hoặc Admin.</summary>
    public static bool CanDelete(string commentAuthorId, string recipeAuthorId, string? currentUserId, bool isAdmin) =>
        isAdmin
        || CanEdit(commentAuthorId, currentUserId)
        || (currentUserId is not null && string.Equals(recipeAuthorId, currentUserId, StringComparison.Ordinal));

    public static CommentDto ToDto(RecipeComment c, ApplicationUser author, string recipeAuthorId, string? currentUserId, bool isAdmin) =>
        new(c.Id, c.RecipeId, c.Content,
            new CommentAuthorDto(author.Id, author.DisplayName, author.AvatarUrl),
            c.CreatedAt, c.UpdatedAt, c.IsEdited,
            CanEdit(c.AuthorId, currentUserId),
            CanDelete(c.AuthorId, recipeAuthorId, currentUserId, isAdmin));
}
