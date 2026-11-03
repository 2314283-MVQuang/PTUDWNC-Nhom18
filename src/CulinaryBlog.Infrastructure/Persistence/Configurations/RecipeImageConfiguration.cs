using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
    public void Configure(EntityTypeBuilder<RecipeImage> builder)
    {
        builder.ToTable("RecipeImages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OriginalUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.MediumUrl).HasMaxLength(500);
        builder.Property(x => x.ThumbnailUrl).HasMaxLength(500);
        builder.Property(x => x.AltText).HasMaxLength(200);

        builder.HasOne(x => x.Recipe)
            .WithMany(r => r.Images)
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chương 3 (3.6): đọc theo RecipeId + ORDER BY OrderIndex → index ghép.
        builder.HasIndex(x => new { x.RecipeId, x.OrderIndex }).HasDatabaseName("IX_RecipeImages_RecipeId_OrderIndex");

        // Chỉ 1 ảnh IsPrimary=true cho mỗi Recipe — partial unique index (khớp
        // "UQ_RecipeImages_OnePrimaryPerRecipe" trong db/init/02-schema.sql).
        builder.HasIndex(x => x.RecipeId)
            .IsUnique()
            .HasFilter("\"IsPrimary\"")
            .HasDatabaseName("UQ_RecipeImages_OnePrimaryPerRecipe");

        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
