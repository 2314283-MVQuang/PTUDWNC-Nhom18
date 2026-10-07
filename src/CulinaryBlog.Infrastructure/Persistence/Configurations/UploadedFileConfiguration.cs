using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class UploadedFileConfiguration : IEntityTypeConfiguration<UploadedFile>
{
    public void Configure(EntityTypeBuilder<UploadedFile> builder)
    {
        builder.ToTable("UploadedFiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.StorageKey)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Url)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.BucketName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.UploadedBy)
            .HasMaxLength(256);

        // KHÔNG dùng IsRowVersion() cho bảng này: các bảng khác dựa vào trigger "touch_row()" của
        // PostgreSQL để sinh RowVersion, còn UploadedFiles được tạo bằng migration (không có trigger).
        // Metadata file chỉ ghi 1 lần, không cần optimistic concurrency — RowVersion giữ giá trị mặc
        // định (mảng rỗng) của BaseEntity để thoả ràng buộc NOT NULL.

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
