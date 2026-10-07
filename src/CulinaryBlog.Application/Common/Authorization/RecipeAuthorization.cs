using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;

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
}