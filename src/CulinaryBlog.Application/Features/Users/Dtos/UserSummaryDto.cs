namespace CulinaryBlog.Application.Features.Users.Dtos;

/// <summary>
/// Tuần 9 (Quang) — 1 dòng trong trang quản lý người dùng của Admin (/dashboard/users).
/// KHÔNG có PasswordHash/SecurityStamp/token. Số công thức không tính bản đã xoá mềm.
/// </summary>
public sealed record UserSummaryDto(
    string Id,
    string Email,
    string? UserName,
    string DisplayName,
    string? AvatarUrl,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt,
    int RecipeCount,
    int PublishedRecipeCount);
