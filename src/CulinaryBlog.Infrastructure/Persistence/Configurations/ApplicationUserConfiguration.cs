using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

/// <summary>Chỉ cấu hình thêm các cột CulinaryBlog thêm vào IdentityUser — base.OnModelCreating
/// trong CulinaryBlogDbContext đã cấu hình đầy đủ các cột/bảng Identity chuẩn.</summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        // FR-USR-001 (/dashboard/users sắp mới nhất trước) — khớp db/init/02-schema.sql.
        builder.HasIndex(x => x.CreatedAt).IsDescending().HasDatabaseName("IX_AspNetUsers_CreatedAt");
    }
}
