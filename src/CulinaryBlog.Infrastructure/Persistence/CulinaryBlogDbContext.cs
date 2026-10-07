using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence;

/// <summary>
/// IdentityDbContext&lt;ApplicationUser&gt; tự tạo đủ 7 bảng Identity chuẩn (AspNetUsers,
/// AspNetRoles, AspNetUserRoles...) đúng tên như trong db/init/02-schema.sql.
///
/// LƯU Ý QUAN TRỌNG: DbContext này KHÔNG map cột "SearchVector" (tsvector) của bảng Recipes —
/// cột đó do trigger PostgreSQL "trg_Recipes_search_vector" tự quản lý hoàn toàn (xem
/// db/init/02-schema.sql). Nếu sau này nhóm chuyển sang dùng "dotnet ef migrations add" để tự
/// sinh schema (thay vì chạy sẵn db/init/*.sql), migration đầu tiên sẽ THIẾU cột này và trigger —
/// xem hướng dẫn phối hợp ở "db/README.md" mục cuối trước khi làm.
/// </summary>
public class CulinaryBlogDbContext(DbContextOptions<CulinaryBlogDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IUnitOfWork
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();

    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UploadedFile> UploadedFiles => Set<UploadedFile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Bắt buộc gọi trước — cấu hình các bảng AspNet* chuẩn.

        builder.ApplyConfigurationsFromAssembly(typeof(CulinaryBlogDbContext).Assembly);

        // Soft delete pattern (mục 7.1): mọi entity kế thừa BaseEntity chỉ query những dòng
        // IsDeleted = false theo mặc định. Recipe cũng có filter này dù thực tế dùng hard-delete
        // (FR-RCP-007) — không sai, chỉ là cột IsDeleted của Recipe luôn là false.
        builder.Entity<Category>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Recipe>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<RecipeStep>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<RecipeIngredient>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<RecipeImage>().HasQueryFilter(x => !x.IsDeleted);
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        // Provider InMemory (chỉ dùng trong unit test) không hỗ trợ transaction — chạy thẳng action.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            await action();
            return;
        }

        await using var transaction = await Database.BeginTransactionAsync(ct);

        await action();

        await transaction.CommitAsync(ct);
        // Không cần Rollback thủ công: nếu action ném exception thì CommitAsync không chạy, và
        // transaction.DisposeAsync() (do "await using") tự rollback phần đã làm dở.
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // AuditInterceptor (mục 6.2): tự set CreatedAt/UpdatedAt khi SaveChanges — xem file
        // Persistence/Interceptors/AuditInterceptor.cs. Không tự set RowVersion — cột đó do
        // trigger "touch_row()" ở DB quản lý (xem BaseEntity.cs).
        optionsBuilder.AddInterceptors(new AuditInterceptor());
        base.OnConfiguring(optionsBuilder);
    }
}
