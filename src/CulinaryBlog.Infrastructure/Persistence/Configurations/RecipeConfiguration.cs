using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("Recipes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();

        builder.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();

        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Instructions).IsRequired();

        builder.Property(x => x.Difficulty).HasConversion<short>();
        builder.Property(x => x.Status).HasConversion<short>();

        builder.HasOne(x => x.Category)
            .WithMany(c => c.Recipes)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict); // CATEGORY_DELETE_HAS_RECIPES — không cho xóa category còn recipe.

        builder.HasOne(x => x.Author)
            .WithMany(a => a.Recipes)
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // RecipeNutrition — Owned Entity, nhúng vào bảng Recipes với tiền tố cột "Nutrition_" (mục 7.2).
        builder.OwnsOne(x => x.Nutrition, nutrition =>
        {
            nutrition.Property(n => n.Calories).HasColumnName("Nutrition_Calories").HasPrecision(8, 2);
            nutrition.Property(n => n.Protein).HasColumnName("Nutrition_Protein").HasPrecision(8, 2);
            nutrition.Property(n => n.Carbohydrates).HasColumnName("Nutrition_Carbohydrates").HasPrecision(8, 2);
            nutrition.Property(n => n.Fat).HasColumnName("Nutrition_Fat").HasPrecision(8, 2);
            nutrition.Property(n => n.Fiber).HasColumnName("Nutrition_Fiber").HasPrecision(8, 2);
            nutrition.Property(n => n.Sodium).HasColumnName("Nutrition_Sodium").HasPrecision(8, 2);
        });
        builder.Navigation(x => x.Nutrition).IsRequired();

        // KHÔNG map cột "SearchVector" — trigger PostgreSQL tự quản lý hoàn toàn (xem
        // db/init/02-schema.sql và ghi chú ở đầu CulinaryBlogDbContext.cs).

        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.AuthorId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Difficulty);

        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
