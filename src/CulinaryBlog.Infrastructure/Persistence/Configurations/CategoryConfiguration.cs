using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();

        builder.Property(x => x.ImageUrl).HasMaxLength(500);

        // RowVersion sinh bởi trigger "touch_row()" ở DB (xem BaseEntity.cs) — EF chỉ đọc lại
        // giá trị mới sau khi lưu, không tự ghi, và dùng nó làm optimistic concurrency token.
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
