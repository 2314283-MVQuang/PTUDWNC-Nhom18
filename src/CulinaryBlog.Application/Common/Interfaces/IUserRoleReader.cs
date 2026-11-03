namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// Chương 3 (3.5.1 — tránh N+1): đọc role của NHIỀU người dùng bằng 1 câu SQL (JOIN
/// AspNetUserRoles + AspNetRoles) thay vì gọi UserManager.GetRolesAsync cho từng người.
/// </summary>
public interface IUserRoleReader
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetRolesAsync(
        IReadOnlyCollection<string> userIds, CancellationToken ct = default);
}
