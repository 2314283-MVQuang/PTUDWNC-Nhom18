using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Users;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Services;

public sealed class UserDirectory(CulinaryBlogDbContext db) : IUserDirectory
{
    public async Task<PagedResult<UserListDto>> GetAsync(GetUsersQuery request, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking();
        var rolesQuery = from link in db.UserRoles join role in db.Roles on link.RoleId equals role.Id
            select new { link.UserId, role.Name };
        if (!string.IsNullOrEmpty(request.Role)) users = users.Where(user => rolesQuery.Any(role => role.UserId == user.Id && role.Name == request.Role));
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var q = request.Q.Trim().ToUpperInvariant();
            users = users.Where(user => user.DisplayName.ToUpper().Contains(q) || (user.Email != null && user.Email.ToUpper().Contains(q)));
        }
        var total = await users.CountAsync(ct);
        var page = await users.OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
            .Skip((request.Page-1)*request.PageSize).Take(request.PageSize)
            .Select(user => new { user.Id, user.Email, user.DisplayName, user.AvatarUrl, user.IsActive, user.CreatedAt }).ToListAsync(ct);
        var ids = page.Select(user => user.Id).ToArray();
        var roles = await rolesQuery.Where(role => ids.Contains(role.UserId)).ToListAsync(ct);
        var items = page.Select(user => new UserListDto(user.Id, user.Email ?? "", user.DisplayName, user.AvatarUrl,
            user.IsActive, user.CreatedAt, roles.Where(role => role.UserId == user.Id).Select(role => role.Name!).Order().ToArray())).ToList();
        return PagedResult<UserListDto>.Create(items, request.Page, request.PageSize, total);
    }
}
