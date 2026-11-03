using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

/// <summary>FR-CMT (Tuần 9): khớp bảng "RecipeComments" trong db/init/02-schema.sql.</summary>
public class RecipeCommentConfiguration : IEntityTypeConfiguration<RecipeComment>
{
    public void Configure(EntityTypeBuilder<RecipeComment> builder)
    {
        builder.ToTable("RecipeComments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content).HasMaxLength(RecipeComment.MaxContentLength).IsRequired();
        builder.Property(x => x.AuthorId).HasMaxLength(450).IsRequired();

        builder.HasOne(x => x.Recipe)
            .WithMany()
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Author)
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Trang công thức đọc bình luận mới nhất trước theo từng công thức.
        builder.HasIndex(x => new { x.RecipeId, x.CreatedAt });
        builder.HasIndex(x => x.AuthorId);

        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
