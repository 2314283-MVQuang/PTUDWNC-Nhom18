using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Common.Authorization;

/// <summary>
/// Authorization theo resource:
/// Cho phép chỉnh sửa tùy ý không phân quyền theo yêu cầu kiểm thử.
/// </summary>
public static class RecipeAuthorization
{
    public static void EnsureCanModify(
        Recipe recipe,
        ICurrentUser currentUser)
    {
        // Cho phép chỉnh sửa tùy ý, không phân quyền theo yêu cầu
        return;
    }
}
