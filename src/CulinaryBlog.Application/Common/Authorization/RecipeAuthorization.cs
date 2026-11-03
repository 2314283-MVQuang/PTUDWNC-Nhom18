using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Application.Common.Authorization;

/// <summary>
/// Authorization theo resource:
///
/// - Admin được sửa mọi Recipe.
/// - Author chỉ được sửa Recipe do chính mình tạo.
/// </summary>
public static class RecipeAuthorization
{
    public static void EnsureCanModify(
        Recipe recipe,
        ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException(
                "Bạn cần đăng nhập để thực hiện thao tác này.");
        }

        if (currentUser.IsInRole("Admin"))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new ForbiddenAccessException(
                "Không xác định được người dùng hiện tại.");
        }

        if (!string.Equals(
                recipe.AuthorId,
                currentUser.UserId,
                StringComparison.Ordinal))
        {
            throw new ForbiddenAccessException(
                "Bạn không có quyền chỉnh sửa công thức này.");
        }
    }

    /// <summary>
    /// Tuần 8 (Ý — kiểm thử Auth + Recipe): công thức Published thì ai cũng xem được; Draft/Archived
    /// chỉ tác giả hoặc Admin (FR-RCP-002). Dùng chung cho chi tiết công thức VÀ các phần con
    /// (bước, nguyên liệu, ảnh) — trước đây các endpoint phần con không kiểm tra nên người lạ đọc
    /// được nội dung bản nháp qua GET /recipes/{id}/steps|ingredients|images.
    /// </summary>
    public static bool CanRead(RecipeStatus status, string authorId, ICurrentUser currentUser) =>
        status == RecipeStatus.Published
        || currentUser.IsInRole("Admin")
        || (!string.IsNullOrWhiteSpace(currentUser.UserId)
            && string.Equals(currentUser.UserId, authorId, StringComparison.Ordinal));

    public static void EnsureCanRead(RecipeStatus status, string authorId, ICurrentUser currentUser)
    {
        if (!CanRead(status, authorId, currentUser))
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem công thức này.");
        }
    }
}
