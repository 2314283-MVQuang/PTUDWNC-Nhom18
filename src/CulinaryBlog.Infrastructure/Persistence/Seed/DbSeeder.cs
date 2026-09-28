using Bogus;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Persistence.Seed;

/// <summary>
/// Sinh dữ liệu MẪU NGẪU NHIÊN bằng thư viện Bogus cho môi trường dev/test — đáp ứng yêu cầu
/// đồ án: tối thiểu 20 categories, 100 recipes, mỗi recipe tối thiểu 10 nguyên liệu và
/// 5 bước chế biến.
///
/// KHÔNG dùng cho production (chỉ được gọi khi app.Environment.IsDevelopment(), xem Program.cs).
///
/// Idempotent: mỗi lần chạy chỉ CHÈN THÊM cho tới khi đạt đủ số lượng tối thiểu, không đụng tới
/// dữ liệu đã có (kể cả 2 tài khoản + 8 category + 3 recipe mẫu từ db/init/03-seed.sql) — chạy
/// lại "dotnet run" nhiều lần vẫn an toàn, không sinh trùng lặp thêm khi đã đủ.
/// </summary>
public static class DbSeeder
{
    private const int MinCategories = 20;
    private const int MinRecipes = 100;
    private const int MinIngredientsPerRecipe = 10;
    private const int MinStepsPerRecipe = 5;

    // -------------------------------------------------------------------------------------
    // Ngân hàng dữ liệu tiếng Việt — dùng để Bogus phối ngẫu nhiên thành tên/nội dung hợp lý,
    // thay vì chữ Latin vô nghĩa (Lorem ipsum) không phù hợp với một blog ẩm thực tiếng Việt.
    // -------------------------------------------------------------------------------------

    private static readonly string[] ExtraCategoryNames =
    [
        "Món hấp", "Món chiên", "Món xào", "Món kho", "Lẩu & Nồi nước",
        "Bánh mặn", "Bánh ngọt", "Mứt & Ô mai", "Nước chấm & Gia vị", "Món ăn vặt",
        "Cơm & Xôi", "Bún, Phở & Mì", "Món cho bé", "Món eat clean", "Món giảm cân",
        "Đồ chua & Dưa muối", "Món Hàn Quốc", "Món Nhật Bản", "Món Thái", "Món Âu",
        "Chay thanh đạm", "Salad & Gỏi trộn", "Món ngày Tết", "Đồ nướng BBQ",
    ];

    private static readonly string[] DishBases =
    [
        "Canh", "Súp", "Gỏi", "Salad", "Chè", "Bánh", "Cháo", "Lẩu", "Nem", "Chả",
        "Cơm chiên", "Bún xào", "Mì xào", "Miến xào", "Xôi", "Gỏi cuốn", "Chả giò",
        "Thịt kho", "Cá kho", "Tôm rang", "Gà nướng", "Bò xào", "Heo quay", "Vịt nấu",
        "Ốc um", "Mực xào", "Đậu hũ sốt", "Rau xào", "Trứng chiên", "Bánh xèo",
        "Cá hấp", "Tôm hấp", "Gà kho", "Bò kho", "Canh chua",
    ];

    private static readonly string[] Proteins =
    [
        "bò", "gà", "heo", "tôm", "cá", "mực", "cua", "ếch", "vịt", "đậu hũ",
        "nấm", "trứng", "chay", "sườn non", "ba chỉ", "cá basa", "tôm sú", "ghẹ",
    ];

    private static readonly string[] Styles =
    [
        "kiểu Huế", "kiểu miền Tây", "kiểu Hà Nội", "sốt cà chua", "sốt me",
        "nước dừa", "rau củ", "tiêu xanh", "sả ớt", "gừng nghệ", "mắm nêm",
        "kiểu Thái", "chua ngọt", "ngũ vị hương", "truyền thống", "kiểu Nhật",
        "sốt tiêu đen", "lá chanh",
    ];

    private static readonly (string Name, string Unit)[] IngredientBank =
    [
        ("Thịt bò", "kg"), ("Thịt heo", "kg"), ("Thịt ba chỉ", "kg"), ("Ức gà", "kg"),
        ("Đùi gà", "kg"), ("Tôm sú", "kg"), ("Cá basa", "kg"), ("Cá lóc", "kg"),
        ("Mực ống", "kg"), ("Nghêu", "kg"), ("Đậu hũ", "miếng"), ("Nấm rơm", "g"),
        ("Nấm hương", "g"), ("Nấm kim châm", "g"), ("Cà rốt", "củ"), ("Khoai tây", "củ"),
        ("Khoai lang", "củ"), ("Hành tây", "củ"), ("Hành lá", "g"), ("Tỏi", "tép"),
        ("Gừng", "nhánh"), ("Sả", "cây"), ("Ớt", "trái"), ("Chanh", "trái"),
        ("Nước mắm", "ml"), ("Dầu ăn", "ml"), ("Dầu hào", "ml"), ("Muối", "g"),
        ("Đường", "g"), ("Tiêu", "g"), ("Bột ngọt", "g"), ("Hạt nêm", "g"),
        ("Nước cốt dừa", "ml"), ("Rau muống", "bó"), ("Cải thìa", "bó"), ("Cải ngọt", "bó"),
        ("Giá đỗ", "g"), ("Bún tươi", "kg"), ("Bánh phở", "kg"), ("Bánh tráng", "cái"),
        ("Trứng gà", "quả"), ("Bột năng", "g"), ("Bột chiên giòn", "g"), ("Me chua", "g"),
        ("Cà chua", "quả"), ("Dưa leo", "quả"), ("Rau thơm", "g"), ("Đậu phộng rang", "g"),
        ("Sữa tươi không đường", "ml"), ("Đậu xanh cà vỏ", "kg"), ("Lá dứa", "lá"), ("Ớt chuông", "trái"),
    ];

    private static readonly string[] StepTitles =
    [
        "Sơ chế nguyên liệu", "Ướp gia vị", "Chuẩn bị nước dùng", "Xào sơ",
        "Nấu chín", "Nêm nếm lại", "Hoàn thiện món ăn", "Trình bày",
        "Làm nước chấm", "Om nhỏ lửa", "Chiên vàng giòn", "Hấp chín",
        "Luộc sơ", "Phi thơm hành tỏi",
    ];

    private static readonly string[] StepDescriptionTemplates =
    [
        "Rửa sạch nguyên liệu chính, để ráo nước rồi thái miếng vừa ăn.",
        "Ướp nguyên liệu với gia vị đã chuẩn bị trong khoảng 15-20 phút cho ngấm đều.",
        "Bắc chảo lên bếp, phi thơm hành tỏi trước khi cho nguyên liệu vào.",
        "Đun lửa vừa đến khi nước sôi thì hạ nhỏ lửa, nấu thêm một lúc cho vừa chín tới.",
        "Nêm nếm lại gia vị cho vừa ăn trước khi tắt bếp.",
        "Múc ra đĩa/tô, trang trí thêm rau thơm rồi thưởng thức khi còn nóng.",
        "Đảo đều tay để món ăn chín đều, tránh bị cháy khét.",
        "Cho thêm chút nước nếu thấy cạn, đậy nắp om đến khi mềm.",
        "Chiên với lửa vừa đến khi vàng đều hai mặt.",
        "Hấp cách thủy khoảng 10-15 phút đến khi chín.",
    ];

    private static readonly string[] DescriptionTemplates =
    [
        "Món {0} thơm ngon, dễ làm, phù hợp cho bữa cơm gia đình.",
        "{0} là lựa chọn quen thuộc, nguyên liệu dễ tìm, thực hiện nhanh gọn.",
        "Công thức {0} chuẩn vị, hướng dẫn chi tiết từng bước cho người mới nấu.",
        "{0} đậm đà hương vị, thích hợp đổi món trong tuần.",
        "Cách làm {0} đơn giản tại nhà, không cần dụng cụ cầu kỳ.",
    ];

    private static readonly string[] InstructionsTemplates =
    [
        "Chuẩn bị đầy đủ nguyên liệu theo danh sách, sơ chế sạch sẽ trước khi bắt tay vào nấu. " +
        "Thực hiện lần lượt theo các bước bên dưới, nêm nếm lại gia vị cho vừa khẩu vị gia đình.",
        "Sơ chế nguyên liệu, ướp gia vị vừa đủ rồi nấu theo từng bước hướng dẫn. " +
        "Lưu ý căn thời gian nấu để món ăn giữ được độ tươi ngon và không bị quá lửa.",
        "Món ăn thực hiện qua vài bước cơ bản: sơ chế, ướp, nấu chín và hoàn thiện. " +
        "Có thể gia giảm gia vị tuỳ khẩu vị từng nhà.",
    ];

    /// <summary>Điểm vào duy nhất — gọi từ Program.cs khi app chạy ở môi trường Development.</summary>
    public static async Task SeedRandomDataAsync(CulinaryBlogDbContext context, ILogger? logger = null, CancellationToken ct = default)
    {
        var faker = new Faker();

        var categoryIds = await SeedCategoriesAsync(context, faker, logger, ct);
        await SeedRecipesAsync(context, faker, categoryIds, logger, ct);
    }

    private static async Task<List<Guid>> SeedCategoriesAsync(
        CulinaryBlogDbContext context, Faker faker, ILogger? logger, CancellationToken ct)
    {
        var existing = await context.Categories
            .IgnoreQueryFilters()
            .Select(c => new { c.Id, c.Name, c.Slug })
            .ToListAsync(ct);

        var currentCount = existing.Count;
        if (currentCount >= MinCategories)
        {
            logger?.LogInformation(
                "[DbSeeder] Đã có {Count} categories (>= {Min}) — bỏ qua bước sinh thêm.",
                currentCount, MinCategories);
            return existing.Select(c => c.Id).ToList();
        }

        var usedNames = new HashSet<string>(existing.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        var usedSlugs = new HashSet<string>(existing.Select(c => c.Slug), StringComparer.OrdinalIgnoreCase);

        var candidates = faker.Random.Shuffle(ExtraCategoryNames).ToList();
        var toCreate = MinCategories - currentCount;
        var newCategories = new List<Category>();

        foreach (var name in candidates)
        {
            if (newCategories.Count >= toCreate)
            {
                break;
            }

            if (!usedNames.Add(name))
            {
                continue; // trùng tên (kể cả đã có trong DB) — bỏ qua.
            }

            var slug = BuildUniqueSlug(name, usedSlugs);

            newCategories.Add(new Category
            {
                Id = Guid.NewGuid(),
                Name = name,
                Slug = slug,
                Description = $"Các công thức thuộc nhóm {name.ToLowerInvariant()}.",
                OrderIndex = currentCount + newCategories.Count + 1,
            });
        }

        if (newCategories.Count > 0)
        {
            context.Categories.AddRange(newCategories);
            await context.SaveChangesAsync(ct);
        }

        logger?.LogInformation(
            "[DbSeeder] Đã sinh thêm {New} categories (tổng {Total}/{Min}).",
            newCategories.Count, currentCount + newCategories.Count, MinCategories);

        return [.. existing.Select(c => c.Id), .. newCategories.Select(c => c.Id)];
    }

    private static async Task SeedRecipesAsync(
        CulinaryBlogDbContext context, Faker faker, List<Guid> categoryIds, ILogger? logger, CancellationToken ct)
    {
        var currentCount = await context.Recipes.IgnoreQueryFilters().CountAsync(ct);
        if (currentCount >= MinRecipes)
        {
            logger?.LogInformation(
                "[DbSeeder] Đã có {Count} recipes (>= {Min}) — bỏ qua bước sinh thêm.",
                currentCount, MinRecipes);
            return;
        }

        var authorIds = await context.Users.Select(u => u.Id).ToListAsync(ct);
        if (authorIds.Count == 0)
        {
            logger?.LogWarning(
                "[DbSeeder] Chưa có tài khoản nào trong AspNetUsers — không thể sinh Recipe vì " +
                "AuthorId là NOT NULL. Hãy chạy db/init/03-seed.sql (tạo 2 tài khoản mẫu) trước.");
            return;
        }

        if (categoryIds.Count == 0)
        {
            logger?.LogWarning("[DbSeeder] Chưa có category nào — không thể sinh Recipe.");
            return;
        }

        var usedTitles = new HashSet<string>(
            await context.Recipes.IgnoreQueryFilters().Select(r => r.Title).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);
        var usedSlugs = new HashSet<string>(
            await context.Recipes.IgnoreQueryFilters().Select(r => r.Slug).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var toCreate = MinRecipes - currentCount;
        var titlePool = BuildTitleCandidates(faker, usedTitles, toCreate);

        var created = 0;
        foreach (var title in titlePool)
        {
            if (created >= toCreate)
            {
                break;
            }

            var recipe = BuildRecipe(faker, title, categoryIds, authorIds, usedSlugs);

            context.Recipes.Add(recipe);

            try
            {
                await context.SaveChangesAsync(ct);
                created++;
            }
            catch (DbUpdateException ex)
            {
                // Hiếm khi xảy ra (đụng UNIQUE do trùng slug/tên với dữ liệu người khác vừa thêm) —
                // bỏ qua bản ghi này, gỡ hết khỏi ChangeTracker (kể cả Ingredients/Steps con) để
                // không ảnh hưởng tới lần SaveChanges kế tiếp.
                foreach (var ingredient in recipe.Ingredients)
                {
                    context.Entry(ingredient).State = EntityState.Detached;
                }

                foreach (var step in recipe.Steps)
                {
                    context.Entry(step).State = EntityState.Detached;
                }

                context.Entry(recipe).State = EntityState.Detached;
                logger?.LogWarning(ex, "[DbSeeder] Bỏ qua 1 recipe do lỗi lưu (có thể trùng slug).");
            }
        }

        logger?.LogInformation(
            "[DbSeeder] Đã sinh thêm {New} recipes (tổng {Total}/{Min}), mỗi recipe >= {Ing} nguyên liệu và >= {Steps} bước.",
            created, currentCount + created, MinRecipes, MinIngredientsPerRecipe, MinStepsPerRecipe);
    }

    private static List<string> BuildTitleCandidates(Faker faker, HashSet<string> usedTitles, int need)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(usedTitles, StringComparer.OrdinalIgnoreCase);

        // 35 base * 18 protein * 18 style ~ 11.000 tổ hợp — thừa sức lấy ngẫu nhiên không lặp.
        var pool = new List<string>();
        foreach (var b in DishBases)
        {
            foreach (var p in Proteins)
            {
                foreach (var s in Styles)
                {
                    pool.Add($"{b} {p} {s}");
                }
            }
        }

        pool = faker.Random.Shuffle(pool).ToList();

        foreach (var title in pool)
        {
            if (candidates.Count >= need)
            {
                break;
            }

            var normalized = $"{char.ToUpper(title[0])}{title[1..]}";
            if (seen.Add(normalized))
            {
                candidates.Add(normalized);
            }
        }

        return candidates;
    }

    private static Recipe BuildRecipe(
        Faker faker, string title, List<Guid> categoryIds, List<string> authorIds, HashSet<string> usedSlugs)
    {
        var slug = BuildUniqueSlug(title, usedSlugs);
        var status = faker.Random.WeightedRandom<RecipeStatus>(
            [RecipeStatus.Published, RecipeStatus.Draft, RecipeStatus.Archived],
            [0.85f, 0.10f, 0.05f]);

        var recipeId = Guid.NewGuid();

        var recipe = new Recipe
        {
            Id = recipeId,
            Title = title,
            Slug = slug,
            Description = string.Format(faker.PickRandom(DescriptionTemplates), title),
            Instructions = faker.PickRandom(InstructionsTemplates),
            PrepTime = faker.Random.Int(10, 60),
            CookTime = faker.Random.Int(0, 120),
            Servings = faker.Random.Int(1, 8),
            Difficulty = faker.PickRandom<RecipeDifficulty>(),
            Status = status,
            CategoryId = faker.PickRandom(categoryIds),
            AuthorId = faker.PickRandom(authorIds),
            PublishedAt = status == RecipeStatus.Published
                ? (DateTimeOffset)faker.Date.Past(1).ToUniversalTime()
                : null,
            Nutrition = new RecipeNutrition
            {
                Calories = faker.Random.Decimal(100, 800),
                Protein = faker.Random.Decimal(5, 50),
                Carbohydrates = faker.Random.Decimal(5, 100),
                Fat = faker.Random.Decimal(2, 40),
                Fiber = faker.Random.Decimal(0, 10),
                Sodium = faker.Random.Decimal(50, 1500),
            },
        };

        var ingredientCount = faker.Random.Int(MinIngredientsPerRecipe, MinIngredientsPerRecipe + 4);
        var pickedIngredients = faker.Random.Shuffle(IngredientBank).Take(ingredientCount).ToList();
        for (var i = 0; i < pickedIngredients.Count; i++)
        {
            var (name, unit) = pickedIngredients[i];
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                Name = name,
                Quantity = faker.Random.Decimal(0.1m, 2m),
                Unit = unit,
                Notes = faker.Random.Bool(0.3f) ? "Sơ chế sạch trước khi dùng" : null,
                OrderIndex = i,
            });
        }

        var stepCount = faker.Random.Int(MinStepsPerRecipe, MinStepsPerRecipe + 2);
        var pickedTitles = faker.Random.Shuffle(StepTitles).Take(stepCount).ToList();
        for (var i = 0; i < stepCount; i++)
        {
            recipe.Steps.Add(new RecipeStep
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                StepNumber = i + 1,
                Title = pickedTitles.Count > i ? pickedTitles[i] : $"Bước {i + 1}",
                Description = faker.PickRandom(StepDescriptionTemplates),
                TimerMinutes = faker.Random.Bool(0.6f) ? faker.Random.Int(5, 60) : null,
            });
        }

        return recipe;
    }

    private static string BuildUniqueSlug(string input, HashSet<string> usedSlugs)
    {
        var baseSlug = SlugHelper.GenerateSlug(input);
        var slug = baseSlug;
        var suffix = 2;

        while (!usedSlugs.Add(slug))
        {
            slug = SlugHelper.AppendSuffix(baseSlug, suffix++);
        }

        return slug;
    }
}
