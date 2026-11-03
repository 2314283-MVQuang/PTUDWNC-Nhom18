using CulinaryBlog.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

public sealed class UserRoleReader(CulinaryBlogDbContext db) : IUserRoleReader
{
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetRolesAsync(
        IReadOnlyCollection<string> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<string>>();
        }

        var ids = userIds.ToList();
        var rows = await (
                from userRole in db.UserRoles.AsNoTracking()
                join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where ids.Contains(userRole.UserId)
                select new { userRole.UserId, role.Name })
            .ToListAsync(ct);

        return rows
            .Where(x => x.Name != null)
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.Name!).OrderBy(n => n, StringComparer.Ordinal).ToList());
    }
}
