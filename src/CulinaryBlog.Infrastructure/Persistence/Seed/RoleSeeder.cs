using Microsoft.Extensions.Configuration;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Persistence.Seed;

/// <summary>
/// Tuần 3 — "phân quyền Role". AddRoles&lt;IdentityRole&gt;() ở DependencyInjection.cs chỉ ĐĂNG KÝ
/// RoleManager vào DI, KHÔNG tự tạo sẵn role nào trong bảng AspNetRoles. Thiếu bước seed này,
/// RegisterCommandHandler gọi userManager.AddToRoleAsync(user, "Author") sẽ thất bại với lỗi
/// Identity "Role Author does not exist" — role phải tồn tại trước khi gán cho user.
///
/// Idempotent (RoleExistsAsync trước khi tạo) nên an toàn khi gọi lại nhiều lần. PHẢI chạy ở MỌI
/// environment kể cả production (khác DbSeeder — DbSeeder chỉ sinh dữ liệu giả, CHỈ chạy ở
/// Development). Gọi 1 lần lúc khởi động ở Program.cs, trước khi any request được xử lý.
/// </summary>
public static class RoleSeeder
{
    /// <summary>2 role chuẩn của hệ thống (mục 5.2 — Author tạo/sửa công thức của mình, Admin
    /// quản trị toàn hệ thống). AuthorizationPolicies.Admin/Author ở API layer dùng đúng 2 tên này.</summary>
    private static readonly string[] Roles = ["Admin", "Author"];

    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        foreach (var roleName in Roles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (result.Succeeded)
            {
                logger.LogInformation("RoleSeeder: đã tạo role {Role}.", roleName);
            }
            else
            {
                // Không throw — lỗi seed role không nên chặn app khởi động (vd chạy lại migration
                // đồng thời trên nhiều instance), chỉ log để dev biết mà kiểm tra thủ công.
                logger.LogError(
                    "RoleSeeder: không tạo được role {Role}: {Errors}",
                    roleName,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    /// <summary>
    /// CHỈ DÙNG Ở DEVELOPMENT (gọi trong khối IsDevelopment() ở Program.cs): tạo sẵn 1 tài khoản
    /// Admin THẬT trong PostgreSQL để test các API cần quyền Admin (sửa công thức của người khác,
    /// tạo/sửa Category...) bằng JWT do backend phát hành. Khác tài khoản mẫu của frontend
    /// (users.json), tài khoản này đăng nhập qua POST /api/v1/auth/login được. Idempotent.
    /// Mật khẩu thoả policy (>= 8 ký tự, 1 hoa, 1 số, 1 ký tự đặc biệt). TUYỆT ĐỐI không gọi ở production.
    /// </summary>
    public static async Task SeedDevAdminAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        const string email = "admin@culinaryblog.local";
        const string password = "Admin@123";

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Quản trị viên (Admin)",
                EmailConfirmed = true,
            };

            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                logger.LogError(
                    "RoleSeeder: không tạo được admin dev: {Errors}",
                    string.Join(", ", create.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("RoleSeeder: đã tạo tài khoản admin dev {Email}.", email);
        }

        foreach (var role in new[] { "Admin", "Author" })
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    /// <summary>
    /// Tuần 8 (triển khai): tạo tài khoản Admin đầu tiên ở môi trường thật từ cấu hình
    /// <c>Admin:Email</c>, <c>Admin:Password</c>, <c>Admin:DisplayName</c> (không có thì bỏ qua).
    /// Tài khoản đã tồn tại thì chỉ đảm bảo có role Admin + Author — KHÔNG đổi mật khẩu.
    /// </summary>
    public static async Task SeedConfiguredAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var email = configuration["Admin:Email"]?.Trim();
        var password = configuration["Admin:Password"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = configuration["Admin:DisplayName"] ?? "Quản trị viên",
                EmailConfirmed = true,
            };

            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                logger.LogError(
                    "RoleSeeder: không tạo được Admin {Email}: {Errors}",
                    email,
                    string.Join(", ", create.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("RoleSeeder: đã tạo tài khoản Admin {Email} từ cấu hình.", email);
        }

        foreach (var role in new[] { "Admin", "Author" })
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
