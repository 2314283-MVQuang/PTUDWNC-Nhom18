using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(CulinaryBlogDbContext dbContext, CancellationToken ct = default)
    {
        try
        {
            // Tạo schema bảng cho SQLite hoặc migrate cho PostgreSQL
            if (dbContext.Database.ProviderName?.Contains("Sqlite") == true)
            {
                await dbContext.Database.EnsureCreatedAsync(ct);
            }
            else if (dbContext.Database.IsRelational())
            {
                try
                {
                    await dbContext.Database.MigrateAsync(ct);
                }
                catch
                {
                    await dbContext.Database.EnsureCreatedAsync(ct);
                }
            }

            // Bỏ qua nếu đã có dữ liệu danh mục
            if (await dbContext.Categories.AnyAsync(ct))
                return;

            var faker = new Faker("vi");
            Randomizer.Seed = new Random(12345);

            // 0. Tạo người dùng tác giả mặc định nếu chưa có
            var defaultUser = await dbContext.Users.FirstOrDefaultAsync(ct);
            if (defaultUser is null)
            {
                defaultUser = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = "culinary_author",
                    NormalizedUserName = "CULINARY_AUTHOR",
                    Email = "author@culinaryblog.com",
                    NormalizedEmail = "AUTHOR@CULINARYBLOG.COM",
                    DisplayName = "Tác Giả Ẩm Thực",
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString()
                };
                await dbContext.Users.AddAsync(defaultUser, ct);
                await dbContext.SaveChangesAsync(ct);
            }

            // 1. Tạo 20 danh mục (Categories)
            var categoryNames = new[]
            {
                "Món Khai Vị", "Món Tráng Miệng", "Món Chính", "Món Xào", "Món Canh",
                "Món Kho", "Món Nướng", "Món Hấp", "Món Chiên", "Món Chay",
                "Ẩm Thực Miền Bắc", "Ẩm Thực Miền Trung", "Ẩm Thực Miền Nam", "Ẩm Thực Á", "Ẩm Thực Âu",
                "Đồ Uống & Pha Chế", "Bánh Ngọt & Bánh Mì", "Món Ăn Vặt", "Món Lẩu", "Món Salad"
            };

            var categories = new List<Category>();
            for (int i = 0; i < categoryNames.Length; i++)
            {
                var name = categoryNames[i];
                categories.Add(new Category
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Slug = $"danh-muc-{i + 1}-{name.ToLowerInvariant().Replace(' ', '-')}",
                    Description = $"Các món ăn thuộc danh mục {name}.",
                    ImageUrl = $"https://picsum.photos/seed/cat{i}/400/300",
                    OrderIndex = i
                });
            }
            await dbContext.Categories.AddRangeAsync(categories, ct);
            await dbContext.SaveChangesAsync(ct);

            var categoryIds = categories.Select(c => c.Id).ToList();

            // 2. Tạo 100 công thức (Recipes)
            var units = new[] { "g", "kg", "ml", "muỗng canh", "muỗng cà phê", "quả", "củ", "nhánh", "lát", "chén" };
            var recipes = new List<Recipe>();

            for (int i = 1; i <= 100; i++)
            {
                var title = $"Công thức món ngon #{i}: {faker.Commerce.ProductName()}";
                var recipe = new Recipe
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    Slug = $"cong-thuc-mon-ngon-{i}-{Guid.NewGuid().ToString()[..8]}",
                    Description = faker.Lorem.Paragraph(),
                    Instructions = faker.Lorem.Paragraphs(2),
                    PrepTime = faker.Random.Int(10, 60),
                    CookTime = faker.Random.Int(15, 120),
                    Servings = faker.Random.Int(2, 6),
                    Difficulty = faker.PickRandom<RecipeDifficulty>(),
                    Status = RecipeStatus.Published,
                    PublishedAt = DateTimeOffset.UtcNow,
                    CategoryId = faker.PickRandom(categoryIds),
                    AuthorId = defaultUser.Id
                };
                recipes.Add(recipe);
            }
            await dbContext.Recipes.AddRangeAsync(recipes, ct);
            await dbContext.SaveChangesAsync(ct);

            // 3. Mỗi công thức tạo ít nhất 10 nguyên liệu và ít nhất 5 bước chế biến
            var ingredients = new List<RecipeIngredient>();
            var steps = new List<RecipeStep>();

            foreach (var recipe in recipes)
            {
                // ≥ 10 nguyên liệu
                int ingredientCount = faker.Random.Int(10, 14);
                for (int j = 1; j <= ingredientCount; j++)
                {
                    ingredients.Add(new RecipeIngredient
                    {
                        Id = Guid.NewGuid(),
                        RecipeId = recipe.Id,
                        Name = $"Nguyên liệu {j}: {faker.Commerce.ProductMaterial()}",
                        Quantity = Math.Round(faker.Random.Decimal(10, 500), 1),
                        Unit = faker.PickRandom(units),
                        Notes = j % 3 == 0 ? "Ướp trước 15 phút" : null,
                        OrderIndex = j
                    });
                }

                // ≥ 5 bước chế biến
                int stepCount = faker.Random.Int(5, 8);
                for (int s = 1; s <= stepCount; s++)
                {
                    steps.Add(new RecipeStep
                    {
                        Id = Guid.NewGuid(),
                        RecipeId = recipe.Id,
                        StepNumber = s,
                        Title = $"Bước {s}: Sơ chế và thực hiện",
                        Description = faker.Lorem.Sentence(10, 5),
                        TimerMinutes = s % 2 == 0 ? faker.Random.Int(5, 20) : null
                    });
                }
            }

            await dbContext.RecipeIngredients.AddRangeAsync(ingredients, ct);
            await dbContext.RecipeSteps.AddRangeAsync(steps, ct);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbInitializer] Chưa thể kết nối hoặc khởi tạo dữ liệu mẫu CSDL: {ex.Message}");
        }
    }
}
