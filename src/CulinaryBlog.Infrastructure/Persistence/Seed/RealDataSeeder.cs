using System.Text.Json;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Persistence.Seed;

/// <summary>
/// Tuần 11 (Quang) — DỮ LIỆU THẬT theo yêu cầu giảng viên: ≥ 10 danh mục, ≥ 50 công thức, mỗi công
/// thức có đủ nguyên liệu, các bước chế biến và ảnh minh hoạ; xoá dữ liệu ngẫu nhiên dùng để test.
///
/// Nội dung nằm trong file nhúng <c>RealData/real-data.json</c>: 10 danh mục × 5 món ăn Việt Nam do
/// nhóm biên soạn. Ảnh là ảnh thật trên Wikimedia Commons (Public domain / CC0 / CC BY / CC BY-SA),
/// được tải MỘT LẦN về MinIO của hệ thống; tác giả và giấy phép ghi ở cuối phần Instructions.
///
/// Chạy nền một lần sau khi API khởi động (không làm chậm lúc mở cổng), chỉ khi cấu hình
/// <c>Seed:RealData = true</c>. Idempotent:
///   1. Ẩn (xoá mềm) dữ liệu ngẫu nhiên do <see cref="DbSeeder"/> sinh ra và 3 công thức mẫu của
///      db/init/03-seed.sql; ẩn các danh mục ngẫu nhiên/mẫu không còn công thức nào.
///   2. Tạo danh mục theo slug — danh mục người dùng tự tạo (trùng slug) được giữ nguyên tên/ảnh.
///   3. Tạo công thức theo slug — công thức đã có thì bỏ qua; món nào chưa có ảnh (lần trước tải
///      lỗi) thì tải bù.
/// Không bao giờ đụng tới công thức người dùng tự đăng.
///
/// Tải ảnh: chỉ gọi tới đúng host commons.wikimedia.org với tên file cố định trong JSON nhúng
/// (không nhận URL từ người dùng nên không có rủi ro SSRF như ghi chú ở GenerateThumbnailJob).
/// </summary>
public sealed class RealDataSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RealDataSeeder> logger) : BackgroundService
{
    private const string ResourceSuffix = "RealData.real-data.json";
    private const string CommonsFilePath = "https://commons.wikimedia.org/wiki/Special:FilePath/";
    private const string AttributionMarker = "Ảnh minh hoạ:";

    /// <summary>Id cố định của 3 công thức mẫu trong db/init/03-seed.sql (dữ liệu test).</summary>
    private static readonly Guid[] SampleRecipeIds =
    [
        Guid.Parse("a1111111-1111-4111-8111-111111111111"),
        Guid.Parse("a2222222-2222-4222-8222-222222222222"),
        Guid.Parse("a3333333-3333-4333-8333-333333333333"),
    ];

    /// <summary>Slug 8 danh mục mẫu trong db/init/03-seed.sql.</summary>
    private static readonly string[] SampleCategorySlugs =
        ["mon-khai-vi", "mon-chinh", "canh-sup", "mon-nuong", "mon-chay", "trang-mieng", "do-uong", "bua-sang"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Seed:RealData", false))
        {
            return;
        }

        // Chờ API khởi động xong (migration, seed role/admin chạy trước trong Program.cs).
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        for (var attempt = 1; attempt <= 5 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await SeedAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[RealDataSeeder] Lần {Attempt}/5 lỗi, thử lại sau 30 giây.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        var data = LoadData();

        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<CulinaryBlogDbContext>();
        var storage = services.GetRequiredService<IFileStorageService>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var author = await FindAuthorAsync(userManager, db, ct);
        if (author is null)
        {
            logger.LogWarning("[RealDataSeeder] Chưa có tài khoản nào để làm tác giả — bỏ qua.");
            return;
        }

        var changed = await HideRandomDataAsync(db, data, ct);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        // Chính sách của Wikimedia yêu cầu User-Agent mô tả rõ ứng dụng.
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "CulinaryBlogSeeder/1.0 (DLU student project; https://github.com/2314283-MVQuang/PTUDWNC-Nhom18)");

        var categoryIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var c in data.Categories)
        {
            var (id, catChanged) = await UpsertCategoryAsync(db, storage, http, author.Id, c, ct);
            categoryIds[c.Slug] = id;
            changed |= catChanged;
        }

        // Thời điểm xuất bản xen kẽ các danh mục (món 1 của mỗi danh mục mới nhất, rồi món 2...) để
        // trang chủ "mới nhất" hiển thị đủ loại món chứ không dồn một danh mục.
        var position = data.Recipes
            .GroupBy(r => r.Category)
            .SelectMany(g => g.Select((r, j) => (r.Slug, Key: j * 100 + data.Categories.FindIndex(c => c.Slug == g.Key))))
            .OrderBy(x => x.Key)
            .Select((x, i) => (x.Slug, i))
            .ToDictionary(x => x.Slug, x => x.i, StringComparer.Ordinal);
        var start = DateTimeOffset.UtcNow;

        var created = 0;
        var newImageIds = new List<Guid>();
        foreach (var r in data.Recipes)
        {
            var publishedAt = start.AddMinutes(-7 * (position[r.Slug] + 1));
            var (wasCreated, imageId) = await UpsertRecipeAsync(
                db, storage, http, author.Id, categoryIds[r.Category], r, publishedAt, ct);
            if (wasCreated) created++;
            if (imageId.HasValue) newImageIds.Add(imageId.Value);
        }
        changed |= created > 0 || newImageIds.Count > 0;

        if (!changed)
        {
            logger.LogInformation("[RealDataSeeder] Dữ liệu thật đã đầy đủ, không có gì thay đổi.");
            return;
        }

        // Sinh ảnh medium/thumbnail bằng job nền sẵn có (FR-JOB-002).
        var thumbnails = services.GetService<IThumbnailJobScheduler>();
        foreach (var imageId in newImageIds)
        {
            try { thumbnails?.EnqueueThumbnailGeneration(imageId); }
            catch (Exception ex) { logger.LogWarning(ex, "[RealDataSeeder] Không xếp được job thumbnail."); }
        }

        var cache = services.GetService<ICacheInvalidator>();
        if (cache is not null)
        {
            foreach (var tag in new[] { "recipes", "categories", "search" })
            {
                await cache.EvictByTagAsync(tag, ct);
            }
        }

        logger.LogInformation(
            "[RealDataSeeder] Xong: tạo {Created} công thức, gắn {Images} ảnh. Tổng {Total} công thức thật.",
            created, newImageIds.Count, data.Recipes.Count);
    }

    private static RealData LoadData()
    {
        var assembly = typeof(RealDataSeeder).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<RealData>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("real-data.json rỗng.");
    }

    /// <summary>Tác giả: Admin trong cấu hình (máy chủ) → Admin bất kỳ → người dùng đầu tiên.</summary>
    private async Task<ApplicationUser?> FindAuthorAsync(
        UserManager<ApplicationUser> userManager, CulinaryBlogDbContext db, CancellationToken ct)
    {
        var email = configuration["Admin:Email"]?.Trim();
        if (!string.IsNullOrEmpty(email))
        {
            var configured = await userManager.FindByEmailAsync(email);
            if (configured is not null) return configured;
        }

        var admins = await userManager.GetUsersInRoleAsync("Admin");
        var admin = admins.OrderBy(u => u.CreatedAt).FirstOrDefault();
        if (admin is not null) return admin;

        return await db.Users.OrderBy(u => u.CreatedAt).FirstOrDefaultAsync(ct);
    }

    /// <summary>Xoá mềm công thức/danh mục ngẫu nhiên (DbSeeder) và mẫu (03-seed.sql).</summary>
    private async Task<bool> HideRandomDataAsync(CulinaryBlogDbContext db, RealData data, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var templates = DbSeeder.InstructionsTemplates;
        var randomRecipes = await db.Recipes
            .Where(r => (r.Instructions != null && templates.Contains(r.Instructions)) || SampleRecipeIds.Contains(r.Id))
            .ToListAsync(ct);
        foreach (var r in randomRecipes)
        {
            r.IsDeleted = true;
            r.DeletedAt = now;
        }
        if (randomRecipes.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        var realSlugs = data.Categories.Select(c => c.Slug).ToHashSet(StringComparer.Ordinal);
        var junkSlugs = DbSeeder.ExtraCategoryNames.Select(SlugHelper.GenerateSlug)
            .Concat(SampleCategorySlugs)
            .Where(s => !realSlugs.Contains(s))
            .ToList();
        var junkCategories = await db.Categories
            .Where(c => junkSlugs.Contains(c.Slug) && !db.Recipes.Any(r => r.CategoryId == c.Id))
            .ToListAsync(ct);
        foreach (var c in junkCategories)
        {
            c.IsDeleted = true;
            c.DeletedAt = now;
        }
        if (junkCategories.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        if (randomRecipes.Count + junkCategories.Count > 0)
        {
            logger.LogInformation("[RealDataSeeder] Đã ẩn {Recipes} công thức và {Categories} danh mục ngẫu nhiên/mẫu.",
                randomRecipes.Count, junkCategories.Count);
        }
        return randomRecipes.Count + junkCategories.Count > 0;
    }

    private async Task<(Guid Id, bool Changed)> UpsertCategoryAsync(
        CulinaryBlogDbContext db, IFileStorageService storage, HttpClient http, string authorId,
        RealCategory c, CancellationToken ct)
    {
        var existing = await db.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Slug == c.Slug, ct);
        var randomNames = DbSeeder.ExtraCategoryNames.ToHashSet(StringComparer.Ordinal);
        var changed = false;

        if (existing is null)
        {
            existing = new Category { Id = Guid.NewGuid(), Name = c.Name, Slug = c.Slug, Description = c.Description, OrderIndex = c.Order };
            db.Categories.Add(existing);
            changed = true;
        }
        else
        {
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                changed = true;
            }
            // Danh mục do DbSeeder/03-seed tạo thì đổi sang tên thật; danh mục người dùng tự tạo giữ nguyên.
            if (randomNames.Contains(existing.Name) || (SampleCategorySlugs.Contains(existing.Slug) && existing.ImageUrl is null))
            {
                existing.Name = c.Name;
                existing.Description = c.Description;
                changed = true;
            }
        }

        if (string.IsNullOrEmpty(existing.ImageUrl) && c.Image is not null)
        {
            var url = await DownloadToStorageAsync(db, storage, http, authorId, $"categories/real/{c.Slug}", c.Image, ct);
            if (url is not null)
            {
                existing.ImageUrl = url;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }
        return (existing.Id, changed);
    }

    private async Task<(bool Created, Guid? ImageId)> UpsertRecipeAsync(
        CulinaryBlogDbContext db, IFileStorageService storage, HttpClient http, string authorId, Guid categoryId,
        RealRecipe r, DateTimeOffset publishedAt, CancellationToken ct)
    {
        var recipe = await db.Recipes
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Slug == r.Slug, ct);
        var created = false;

        if (recipe is null)
        {
            // Slug có thể đang bị một dòng đã xoá mềm (dữ liệu ngẫu nhiên) giữ — đổi slug dòng đó đi.
            var hidden = await db.Recipes.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Slug == r.Slug && x.IsDeleted, ct);
            if (hidden is not null)
            {
                hidden.Slug = $"{r.Slug}-cu-{hidden.Id.ToString("N")[..6]}";
                await db.SaveChangesAsync(ct);
            }

            var id = Guid.NewGuid();
            recipe = new Recipe
            {
                Id = id,
                Title = r.Title,
                Slug = r.Slug,
                Description = r.Description,
                Instructions = BuildInstructions(r),
                PrepTime = r.PrepTime,
                CookTime = r.CookTime,
                Servings = r.Servings,
                Difficulty = (RecipeDifficulty)r.Difficulty,
                CategoryId = categoryId,
                AuthorId = authorId,
            };
            for (var i = 0; i < r.Ingredients.Count; i++)
            {
                var ing = r.Ingredients[i];
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Id = Guid.NewGuid(), RecipeId = id, Name = ing.Name, Quantity = ing.Quantity,
                    Unit = ing.Unit, Notes = ing.Notes, OrderIndex = i,
                });
            }
            for (var i = 0; i < r.Steps.Count; i++)
            {
                var step = r.Steps[i];
                recipe.Steps.Add(new RecipeStep
                {
                    Id = Guid.NewGuid(), RecipeId = id, StepNumber = i + 1, Title = step.Title,
                    Description = step.Description, TimerMinutes = step.TimerMinutes,
                });
            }
            // Đủ bước + nguyên liệu nên Publish() hợp lệ (quy tắc FR-RCP-005 trong Domain).
            recipe.Publish();
            recipe.PublishedAt = publishedAt;
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync(ct);
            created = true;
        }

        if (recipe.Images.Any() || r.Image is null)
        {
            return (created, null);
        }

        var url = await DownloadToStorageAsync(db, storage, http, authorId, $"recipes/real/{r.Slug}", r.Image, ct);
        if (url is null)
        {
            return (created, null);
        }

        var image = new RecipeImage
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            OriginalUrl = url,
            AltText = r.Title,
            IsPrimary = true,
            OrderIndex = 0,
        };
        db.RecipeImages.Add(image);
        await db.SaveChangesAsync(ct);
        return (created, image.Id);
    }

    private static string BuildInstructions(RealRecipe r)
    {
        var steps = string.Join(" → ", r.Steps.Select(s => s.Title));
        var credit = r.Image is null
            ? string.Empty
            : $"\n\n{AttributionMarker} {r.Image.Author} – Wikimedia Commons ({r.Image.License}).";
        return $"Các bước chính: {steps}.{credit}";
    }

    /// <summary>Tải ảnh từ Wikimedia Commons (rộng 960px) rồi lưu vào MinIO + bảng UploadedFiles.</summary>
    private async Task<string?> DownloadToStorageAsync(
        CulinaryBlogDbContext db, IFileStorageService storage, HttpClient http, string authorId,
        string keyWithoutExtension, RealImage image, CancellationToken ct)
    {
        var source = CommonsFilePath + Uri.EscapeDataString(image.File) + "?width=960";
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(source, ct);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                var extension = contentType switch
                {
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    _ => ".jpg",
                };
                var key = keyWithoutExtension + extension;

                using var ms = new MemoryStream(bytes);
                var url = await storage.UploadFileAsync(key, ms, contentType, ct);

                var file = await db.UploadedFiles.FirstOrDefaultAsync(f => f.StorageKey == key, ct);
                if (file is null)
                {
                    db.UploadedFiles.Add(new UploadedFile
                    {
                        Id = Guid.NewGuid(),
                        FileName = image.File,
                        ContentType = contentType,
                        Size = bytes.LongLength,
                        StorageKey = key,
                        Url = url,
                        BucketName = storage.BucketName,
                        UploadedBy = authorId,
                    });
                    await db.SaveChangesAsync(ct);
                }

                await Task.Delay(500, ct); // lịch sự với máy chủ Wikimedia
                return url;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "[RealDataSeeder] Tải ảnh {File} lỗi (lần {Attempt}/3).", image.File, attempt);
                await Task.Delay(TimeSpan.FromSeconds(3 * attempt), ct);
            }
        }
        return null;
    }

    // ---------------- Định dạng file real-data.json ----------------
    internal sealed record RealData(List<RealCategory> Categories, List<RealRecipe> Recipes);

    internal sealed record RealCategory(string Slug, string Name, string Description, int Order, RealImage? Image);

    internal sealed record RealImage(string File, string Author, string License);

    internal sealed record RealRecipe(
        string Slug, string Title, string Category, string Description,
        int PrepTime, int CookTime, int Servings, int Difficulty, RealImage? Image,
        List<RealIngredient> Ingredients, List<RealStep> Steps);

    internal sealed record RealIngredient(string Name, decimal? Quantity, string? Unit, string? Notes);

    internal sealed record RealStep(string Title, string Description, int? TimerMinutes);
}
