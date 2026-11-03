using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Users.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Users.Queries.GetUsers;

public sealed class GetUsersQueryHandler(
    UserManager<ApplicationUser> userManager,
    IRepository<Recipe> recipes,
    IUserRoleReader roleReader)
    : IRequestHandler<GetUsersQuery, PagedResult<UserSummaryDto>>
{
    public async Task<PagedResult<UserSummaryDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        // Số Admin rất ít nên lấy 1 lần rồi lọc theo Id — tránh join bảng AspNetUserRoles từ Application.
        var adminIds = (await userManager.GetUsersInRoleAsync(GetUsersQuery.AdminRole))
            .Select(u => u.Id)
            .ToList();

        var query = userManager.Users.AsNoTracking();

        var keyword = request.Keyword?.Trim().ToLower();
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(keyword))
                || (u.UserName != null && u.UserName.ToLower().Contains(keyword))
                || u.DisplayName.ToLower().Contains(keyword));
        }

        if (request.Role == GetUsersQuery.AdminRole)
        {
            query = query.Where(u => adminIds.Contains(u.Id));
        }
        else if (request.Role == GetUsersQuery.AuthorRole)
        {
            query = query.Where(u => !adminIds.Contains(u.Id));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == request.IsActive.Value);
        }

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Email)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var userIds = users.Select(u => u.Id).ToList();
        var counts = await recipes.Query()
            .Where(r => userIds.Contains(r.AuthorId))
            .GroupBy(r => r.AuthorId)
            .Select(g => new
            {
                AuthorId = g.Key,
                Total = g.Count(),
                Published = g.Count(r => r.Status == RecipeStatus.Published),
            })
            .ToDictionaryAsync(x => x.AuthorId, ct);

        // Chương 3 (3.5.1 — N+1): trước đây gọi GetRolesAsync cho TỪNG người (20 người = 20 query).
        // Giờ 1 câu JOIN AspNetUserRoles + AspNetRoles cho cả trang.
        var rolesByUser = await roleReader.GetRolesAsync(userIds, ct);

        var items = new List<UserSummaryDto>(users.Count);
        foreach (var user in users)
        {
            var roles = rolesByUser.TryGetValue(user.Id, out var found) ? found : Array.Empty<string>();
            counts.TryGetValue(user.Id, out var count);
            items.Add(new UserSummaryDto(
                user.Id,
                user.Email ?? string.Empty,
                user.UserName,
                user.DisplayName,
                user.AvatarUrl,
                roles,
                user.IsActive,
                user.EmailConfirmed,
                user.CreatedAt,
                count?.Total ?? 0,
                count?.Published ?? 0));
        }

        return PagedResult<UserSummaryDto>.Create(items, request.Page, request.PageSize, total);
    }
}
