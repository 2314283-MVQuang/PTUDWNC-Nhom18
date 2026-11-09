using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeImage;
using CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeIngredient;
using CulinaryBlog.Application.Features.Recipes.Commands.AddRecipeStep;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeImage;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeIngredient;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeStep;
using CulinaryBlog.Application.Features.Recipes.Commands.SetPrimaryRecipeImage;
using CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipeIngredient;
using CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipeStep;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeImages;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeIngredients;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeSteps;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

public class TestCurrentUser : ICurrentUser
{
    public string? UserId { get; set; } = "user-author-1";
    public string? Email { get; set; } = "author@dlu.edu.vn";
    public bool IsAuthenticated => !string.IsNullOrEmpty(UserId);
    public string Role { get; set; } = "Author";
    public bool IsInRole(string role) => string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
}

public class TestCacheInvalidator : ICacheInvalidator
{
    public List<string> EvictedTags { get; } = [];

    public Task EvictByTagAsync(string tag, CancellationToken ct = default)
    {
        EvictedTags.Add(tag);
        return Task.CompletedTask;
    }
}

public class TestRecipeFileStorageService : IFileStorageService
{
    public string BucketName => "recipe-images";
    public List<string> DeletedObjects { get; } = [];
    /// <summary>Nội dung các object "đã upload" (Tuần 8: AddRecipeImage đọc magic bytes).</summary>
    public Dictionary<string, byte[]> Objects { get; } = new();

    public Task<string> GeneratePresignedUploadUrlAsync(
        string key,
        string contentType,
        int expirySeconds = 900,
        CancellationToken ct = default) =>
        Task.FromResult($"http://minio:9000/{BucketName}/{key}");

    public string GetPublicUrl(string key) => $"http://minio:9000/{BucketName}/{key}";

    public Task<Stream> GetFileStreamAsync(string key, CancellationToken ct = default) =>
        Objects.TryGetValue(key, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new FileNotFoundException(key);

    public Task<string> UploadFileAsync(string key, Stream content, string contentType, CancellationToken ct = default) =>
        Task.FromResult(GetPublicUrl(key));

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        DeletedObjects.Add(objectName);
        return Task.CompletedTask;
    }
}

/// <summary>
/// FR-FILE-002: ghi lại object được lên lịch xoá, kèm trạng thái DB lúc đó để kiểm chứng việc xoá
/// file chỉ được lên lịch SAU khi SaveChanges thành công.
/// </summary>
public class TestFileDeletionScheduler(Func<bool>? isCommitted = null) : IFileDeletionScheduler
{
    public List<string> Scheduled { get; } = [];
    public bool? CommittedWhenScheduled { get; private set; }

    public int Enqueue(IEnumerable<string> objectNames)
    {
        CommittedWhenScheduled = isCommitted?.Invoke();
        var names = objectNames.ToList();
        Scheduled.AddRange(names);
        return names.Count;
    }
}

public class RecipeItemTests
{
    private CulinaryBlogDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<CulinaryBlogDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new CulinaryBlogDbContext(options);
    }

    private async Task<Recipe> SeedRecipeAsync(CulinaryBlogDbContext db, string authorId = "user-author-1")
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Title = "Phở Bò Hà Nội",
            Slug = "pho-bo-ha-noi",
            Description = "Món phở truyền thống chuẩn vị",
            Instructions = "Nấu nước dùng rồi chần phở",
            AuthorId = authorId,
            CategoryId = Guid.NewGuid(),
            Status = RecipeStatus.Draft,
        };
        await db.Recipes.AddAsync(recipe);
        await db.SaveChangesAsync();
        return recipe;
    }

    // ==========================================================
    // 1. INGREDIENTS TESTS (FR-RCP-009)
    // ==========================================================

    [Fact]
    public async Task AddIngredient_ShouldAddSuccessfully_WhenAuthorCalls()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };
        var cacheInv = new TestCacheInvalidator();

        var handler = new AddRecipeIngredientCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeIngredient>(db),
            db,
            currentUser,
            cacheInv);

        var command = new AddRecipeIngredientCommand(recipe.Id, "Thịt bò thăn", 0.5m, "kg", "Thái mỏng vừa ăn");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Thịt bò thăn", result.Name);
        Assert.Equal(0.5m, result.Quantity);
        Assert.Equal("kg", result.Unit);
        Assert.Equal("Thái mỏng vừa ăn", result.Notes);
        Assert.Equal(0, result.OrderIndex);

        var saved = await db.RecipeIngredients.FirstOrDefaultAsync(i => i.Id == result.Id);
        Assert.NotNull(saved);
        Assert.Contains($"recipe-{recipe.Id}", cacheInv.EvictedTags);
    }

    [Fact]
    public async Task AddIngredient_ShouldThrowForbidden_WhenOtherUserCalls()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db, "author-1");
        var currentUser = new TestCurrentUser { UserId = "other-user", Role = "Author" };

        var handler = new AddRecipeIngredientCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeIngredient>(db),
            db,
            currentUser);

        var command = new AddRecipeIngredientCommand(recipe.Id, "Hành lá", 50, "g");
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateIngredient_ShouldUpdateSuccessfully()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var ing = new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            Name = "Thịt bò",
            Quantity = 0.3m,
            Unit = "kg",
            OrderIndex = 0
        };
        await db.RecipeIngredients.AddAsync(ing);
        await db.SaveChangesAsync();

        var handler = new UpdateRecipeIngredientCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeIngredient>(db),
            db,
            currentUser);

        var command = new UpdateRecipeIngredientCommand(recipe.Id, ing.Id, "Thịt bò nạm", 0.4m, "kg", "Cắt khối", 1);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Thịt bò nạm", result.Name);
        Assert.Equal(0.4m, result.Quantity);
        Assert.Equal("Cắt khối", result.Notes);
        Assert.Equal(1, result.OrderIndex);
    }

    [Fact]
    public async Task DeleteIngredient_ShouldSoftDelete()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var ing = new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            Name = "Hạt nêm",
            OrderIndex = 0
        };
        await db.RecipeIngredients.AddAsync(ing);
        await db.SaveChangesAsync();

        var handler = new DeleteRecipeIngredientCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeIngredient>(db),
            db,
            currentUser);

        await handler.Handle(new DeleteRecipeIngredientCommand(recipe.Id, ing.Id), CancellationToken.None);

        var inDb = await db.RecipeIngredients.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == ing.Id);
        Assert.NotNull(inDb);
        Assert.True(inDb.IsDeleted);
    }

    [Fact]
    public async Task GetIngredients_ShouldReturnOrderedList()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);

        await db.RecipeIngredients.AddRangeAsync(
            new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = recipe.Id, Name = "Bánh phở", OrderIndex = 2 },
            new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = recipe.Id, Name = "Xương bò", OrderIndex = 0 },
            new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = recipe.Id, Name = "Hành tây", OrderIndex = 1 }
        );
        await db.SaveChangesAsync();

        var handler = new GetRecipeIngredientsQueryHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeIngredient>(db),
            new TestCurrentUser { UserId = recipe.AuthorId });

        var list = await handler.Handle(new GetRecipeIngredientsQuery(recipe.Id), CancellationToken.None);

        Assert.Equal(3, list.Count);
        Assert.Equal("Xương bò", list[0].Name);
        Assert.Equal("Hành tây", list[1].Name);
        Assert.Equal("Bánh phở", list[2].Name);
    }

    /// <summary>Tuần 8 (Ý): người lạ / tác giả khác không đọc được phần con của bản nháp.</summary>
    [Fact]
    public async Task GetParts_ShouldForbidStrangers_WhenRecipeIsDraft()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db); // Draft
        var anonymous = new TestCurrentUser { UserId = null, Role = "" };
        var otherAuthor = new TestCurrentUser { UserId = "user-author-2" };

        foreach (var user in new[] { anonymous, otherAuthor })
        {
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => new GetRecipeIngredientsQueryHandler(
                new RepositoryBase<Recipe>(db), new RepositoryBase<RecipeIngredient>(db), user)
                .Handle(new GetRecipeIngredientsQuery(recipe.Id), CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => new GetRecipeStepsQueryHandler(
                new RepositoryBase<Recipe>(db), new RepositoryBase<RecipeStep>(db), user)
                .Handle(new GetRecipeStepsQuery(recipe.Id), CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => new GetRecipeImagesQueryHandler(
                new RepositoryBase<Recipe>(db), new RepositoryBase<RecipeImage>(db), user)
                .Handle(new GetRecipeImagesQuery(recipe.Id), CancellationToken.None));
        }

        // Admin vẫn xem được.
        var admin = new TestCurrentUser { UserId = "admin-1", Role = "Admin" };
        var steps = await new GetRecipeStepsQueryHandler(
            new RepositoryBase<Recipe>(db), new RepositoryBase<RecipeStep>(db), admin)
            .Handle(new GetRecipeStepsQuery(recipe.Id), CancellationToken.None);
        Assert.NotNull(steps);
    }

    // ==========================================================
    // 2. STEPS TESTS (FR-RCP-010)
    // ==========================================================

    [Fact]
    public async Task AddStep_ShouldAutoIncrementStepNumber_WhenNotSpecified()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var handler = CreateAddStepHandler(db, currentUser, new TestRecipeFileStorageService());

        var step1 = await handler.Handle(new AddRecipeStepCommand(recipe.Id, "Sơ chế xương", "Rửa sạch ngâm nước muối"), CancellationToken.None);
        var step2 = await handler.Handle(new AddRecipeStepCommand(recipe.Id, "Ninh xương", "Ninh nhỏ lửa 4 tiếng", TimerMinutes: 240), CancellationToken.None);

        Assert.Equal(1, step1.StepNumber);
        Assert.Equal(2, step2.StepNumber);
    }

    [Fact]
    public async Task DeleteStep_ShouldRenumberRemainingSteps()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var step1 = new RecipeStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 1, Title = "B1", Description = "D1" };
        var step2 = new RecipeStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 2, Title = "B2", Description = "D2" };
        var step3 = new RecipeStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 3, Title = "B3", Description = "D3" };
        await db.RecipeSteps.AddRangeAsync(step1, step2, step3);
        await db.SaveChangesAsync();

        var handler = new DeleteRecipeStepCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeStep>(db),
            db,
            currentUser);

        // Xóa step 2
        await handler.Handle(new DeleteRecipeStepCommand(recipe.Id, step2.Id), CancellationToken.None);

        var remaining = await db.RecipeSteps.Where(s => s.RecipeId == recipe.Id).OrderBy(s => s.StepNumber).ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.Equal(step1.Id, remaining[0].Id);
        Assert.Equal(1, remaining[0].StepNumber);

        Assert.Equal(step3.Id, remaining[1].Id);
        Assert.Equal(2, remaining[1].StepNumber); // Đã được renumber từ 3 thành 2!
    }

    [Fact]
    public async Task UpdateStep_ShouldUpdateFieldsSuccessfully()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var step = new RecipeStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 1, Title = "Title Cũ", Description = "Desc Cũ" };
        await db.RecipeSteps.AddAsync(step);
        await db.SaveChangesAsync();

        var storage = new TestRecipeFileStorageService();
        await SeedUploadAsync(db, storage, "recipes/2026/11/step.jpg", Jpeg, recipe.AuthorId);
        var handler = CreateUpdateStepHandler(db, currentUser, storage);

        var stepImage = storage.GetPublicUrl("recipes/2026/11/step.jpg");
        var result = await handler.Handle(new UpdateRecipeStepCommand(recipe.Id, step.Id, "Title Mới", "Desc Mới", TimerMinutes: 15, ImageUrl: stepImage), CancellationToken.None);

        Assert.Equal("Title Mới", result.Title);
        Assert.Equal("Desc Mới", result.Description);
        Assert.Equal(15, result.TimerMinutes);
        Assert.Equal(stepImage, result.ImageUrl);
    }

    // ==========================================================
    // 3. IMAGES TESTS (FR-RCP-008)
    // ==========================================================

    [Fact]
    public async Task AddImage_ShouldBePrimary_WhenFirstImage()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var storage = new TestRecipeFileStorageService();
        await SeedUploadAsync(db, storage, "recipes/2026/10/pho1.jpg", Jpeg, recipe.AuthorId);
        var handler = CreateAddImageHandler(db, currentUser, storage);

        var command = new AddRecipeImageCommand(recipe.Id, "http://minio:9000/recipe-images/recipes/2026/10/pho1.jpg", "Tô phở thơm ngon");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsPrimary);
    }

    // Tuần 8 (Thịnh) — ảnh phải do chính người dùng tải lên bucket của hệ thống, nội dung đúng là ảnh.
    internal static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];

    internal static async Task SeedUploadAsync(
        CulinaryBlogDbContext db, TestRecipeFileStorageService storage, string key, byte[] content, string? uploadedBy)
    {
        storage.Objects[key] = content;
        db.UploadedFiles.Add(new UploadedFile
        {
            FileName = "anh.jpg", ContentType = "image/jpeg", Size = content.Length, StorageKey = key,
            Url = storage.GetPublicUrl(key), BucketName = storage.BucketName, UploadedBy = uploadedBy,
        });
        await db.SaveChangesAsync();
    }

    internal static RecipeImageUploadGuard CreateImageGuard(
        CulinaryBlogDbContext db, TestCurrentUser user, TestRecipeFileStorageService storage) =>
        new(storage, new UploadedFileRepository(db), new RepositoryBase<RecipeImage>(db),
            new RepositoryBase<RecipeStep>(db), new RepositoryBase<Category>(db), user);

    private static AddRecipeImageCommandHandler CreateAddImageHandler(
        CulinaryBlogDbContext db, TestCurrentUser user, TestRecipeFileStorageService storage) =>
        new(new RepositoryBase<Recipe>(db), new RepositoryBase<RecipeImage>(db), db, user,
            CreateImageGuard(db, user, storage));

    private static AddRecipeStepCommandHandler CreateAddStepHandler(
        CulinaryBlogDbContext db, TestCurrentUser user, TestRecipeFileStorageService storage) =>
        new(new RepositoryBase<Recipe>(db), new RepositoryBase<RecipeStep>(db), db, user,
            CreateImageGuard(db, user, storage));

    private static UpdateRecipeStepCommandHandler CreateUpdateStepHandler(
        CulinaryBlogDbContext db, TestCurrentUser user, TestRecipeFileStorageService storage,
        TestFileDeletionScheduler? scheduler = null) =>
        new(new RepositoryBase<Recipe>(db), new RepositoryBase<RecipeStep>(db), db, user,
            CreateImageGuard(db, user, storage), fileDeletionScheduler: scheduler);

    [Fact]
    public async Task AddImage_ShouldRejectExternalUrl()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var handler = CreateAddImageHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, new TestRecipeFileStorageService());

        await Assert.ThrowsAsync<InvalidFileException>(() => handler.Handle(
            new AddRecipeImageCommand(recipe.Id, "https://example.com/anh-ngoai.jpg", null), CancellationToken.None));
    }

    [Fact]
    public async Task AddImage_ShouldRejectFileUploadedByAnotherUser()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var storage = new TestRecipeFileStorageService();
        await SeedUploadAsync(db, storage, "recipes/2026/10/cua-nguoi-khac.jpg", Jpeg, "user-author-2");
        var handler = CreateAddImageHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, storage);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new AddRecipeImageCommand(recipe.Id, storage.GetPublicUrl("recipes/2026/10/cua-nguoi-khac.jpg"), null),
            CancellationToken.None));
    }

    [Fact]
    public async Task AddImage_ShouldRejectSameUploadAttachedTwice_EvenByAdmin()
    {
        // Kiểm thử người dùng thật (Tuần 8): Admin gắn lại ảnh của Phở vào Bún chả rồi xoá ở Bún chả
        // → job xoá file trên MinIO làm ảnh Phở bị 404. Giờ chặn ngay từ lúc gắn lần 2.
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var storage = new TestRecipeFileStorageService();
        const string key = "recipes/2026/11/pho-dung-chung.jpg";
        await SeedUploadAsync(db, storage, key, Jpeg, recipe.AuthorId);
        var url = storage.GetPublicUrl(key);

        await CreateAddImageHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, storage)
            .Handle(new AddRecipeImageCommand(recipe.Id, url, null), CancellationToken.None);

        var admin = new TestCurrentUser { UserId = "admin-1", Role = "Admin" };
        await Assert.ThrowsAsync<ConflictException>(() => CreateAddImageHandler(db, admin, storage)
            .Handle(new AddRecipeImageCommand(recipe.Id, url, null), CancellationToken.None));
        Assert.Single(db.RecipeImages);
    }

    [Fact]
    public async Task AddImage_ShouldRejectAndDeleteFakeImage_ByMagicBytes()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var storage = new TestRecipeFileStorageService();
        // File thực thi Windows (MZ) đổi đuôi thành .png.
        await SeedUploadAsync(db, storage, "recipes/2026/10/virus.png", [0x4D, 0x5A, 0x90, 0, 3, 0, 0, 0, 4, 0, 0, 0], recipe.AuthorId);
        var handler = CreateAddImageHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, storage);

        await Assert.ThrowsAsync<InvalidFileException>(() => handler.Handle(
            new AddRecipeImageCommand(recipe.Id, storage.GetPublicUrl("recipes/2026/10/virus.png"), null), CancellationToken.None));
        Assert.Contains("recipes/2026/10/virus.png", storage.DeletedObjects);
        Assert.Empty(db.RecipeImages);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xDB, 0, 0, 0, 0, 0, 0, 0, 0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 }, "image/png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0, 0, 0, 0x1C, 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x66 }, "image/avif")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0, 0, 0, 0, 0 }, null)] // GIF không được nhận
    [InlineData(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C, 0x3E, 0, 0, 0, 0, 0, 0 }, null)] // <html>
    public void ImageFileSignature_ShouldDetectRealImageTypes(byte[] header, string? expected)
    {
        Assert.Equal(expected, ImageFileSignature.Detect(header));
    }

    [Fact]
    public async Task SetPrimaryImage_ShouldEnsureOnlyOnePrimaryImage()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var img1 = new RecipeImage { Id = Guid.NewGuid(), RecipeId = recipe.Id, OriginalUrl = "url1", IsPrimary = true };
        var img2 = new RecipeImage { Id = Guid.NewGuid(), RecipeId = recipe.Id, OriginalUrl = "url2", IsPrimary = false };
        await db.RecipeImages.AddRangeAsync(img1, img2);
        await db.SaveChangesAsync();

        var handler = new SetPrimaryRecipeImageCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeImage>(db),
            db,
            currentUser);

        var result = await handler.Handle(new SetPrimaryRecipeImageCommand(recipe.Id, img2.Id), CancellationToken.None);

        Assert.True(result.IsPrimary);

        var reloadedImg1 = await db.RecipeImages.FindAsync(img1.Id);
        var reloadedImg2 = await db.RecipeImages.FindAsync(img2.Id);

        Assert.False(reloadedImg1!.IsPrimary);
        Assert.True(reloadedImg2!.IsPrimary);
    }

    [Fact]
    public async Task DeleteImage_ShouldDesignateNextPrimary_WhenDeletedWasPrimary()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(databaseName);
        var recipe = await SeedRecipeAsync(db);
        var currentUser = new TestCurrentUser { UserId = recipe.AuthorId };

        var img1 = new RecipeImage
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            OriginalUrl = $"http://minio:9000/recipe-images/recipes/{recipe.Id}/original.jpg",
            MediumUrl = $"http://minio:9000/recipe-images/recipes/{recipe.Id}/medium.jpg",
            ThumbnailUrl = $"http://minio:9000/recipe-images/recipes/{recipe.Id}/thumbnail.jpg",
            IsPrimary = true,
            OrderIndex = 0
        };
        var img2 = new RecipeImage
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            OriginalUrl = "url2",
            IsPrimary = false,
            OrderIndex = 1
        };
        await db.RecipeImages.AddRangeAsync(img1, img2);
        await db.SaveChangesAsync();

        var storage = new TestRecipeFileStorageService();
        // Đọc DB bằng context RIÊNG: chỉ thấy IsDeleted = true nếu handler đã SaveChanges.
        var scheduler = new TestFileDeletionScheduler(() =>
        {
            using var check = new CulinaryBlogDbContext(
                new DbContextOptionsBuilder<CulinaryBlogDbContext>().UseInMemoryDatabase(databaseName).Options);
            return check.RecipeImages.IgnoreQueryFilters().Single(i => i.Id == img1.Id).IsDeleted;
        });
        var handler = new DeleteRecipeImageCommandHandler(
            new RepositoryBase<Recipe>(db),
            new RepositoryBase<RecipeImage>(db),
            db,
            currentUser,
            storage,
            scheduler);

        await handler.Handle(new DeleteRecipeImageCommand(recipe.Id, img1.Id), CancellationToken.None);

        var reloadedImg1 = await db.RecipeImages.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == img1.Id);
        var reloadedImg2 = await db.RecipeImages.FindAsync(img2.Id);

        // Không xoá trực tiếp trong request — chỉ lên lịch Hangfire, và chỉ sau khi đã lưu DB.
        Assert.Empty(storage.DeletedObjects);
        Assert.Equal(
            [$"recipes/{recipe.Id}/original.jpg", $"recipes/{recipe.Id}/medium.jpg", $"recipes/{recipe.Id}/thumbnail.jpg"],
            scheduler.Scheduled);
        Assert.True(scheduler.CommittedWhenScheduled);
        Assert.True(reloadedImg1!.IsDeleted);
        Assert.False(reloadedImg1.IsPrimary);
        Assert.True(reloadedImg2!.IsPrimary);
    }

    // ==========================================================
    // 4. ẢNH MINH HOẠ TỪNG BƯỚC (FR-RCP-010, Tuần 10 — Quang)
    // ==========================================================

    [Fact]
    public async Task AddStep_WithOwnUploadedImage_StoresImageUrl()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var user = new TestCurrentUser { UserId = recipe.AuthorId };
        var storage = new TestRecipeFileStorageService();
        await SeedUploadAsync(db, storage, "recipes/2026/11/so-che.jpg", Jpeg, recipe.AuthorId);
        var url = storage.GetPublicUrl("recipes/2026/11/so-che.jpg");

        var step = await CreateAddStepHandler(db, user, storage).Handle(
            new AddRecipeStepCommand(recipe.Id, "Sơ chế thịt", "Bóp bột mì rồi rửa sạch", ImageUrl: url), CancellationToken.None);

        Assert.Equal(url, step.ImageUrl);
        Assert.Equal(url, (await db.RecipeSteps.SingleAsync()).ImageUrl);
    }

    [Fact]
    public async Task AddStep_WithExternalImageUrl_IsRejected()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var handler = CreateAddStepHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, new TestRecipeFileStorageService());

        await Assert.ThrowsAsync<InvalidFileException>(() => handler.Handle(
            new AddRecipeStepCommand(recipe.Id, "B1", "D1", ImageUrl: "https://example.com/anh-ngoai.jpg"), CancellationToken.None));
        Assert.Empty(db.RecipeSteps);
    }

    [Fact]
    public async Task AddStep_WithImageUploadedByAnotherUser_IsForbidden()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var storage = new TestRecipeFileStorageService();
        await SeedUploadAsync(db, storage, "recipes/2026/11/cua-nguoi-khac.jpg", Jpeg, "user-author-2");
        var handler = CreateAddStepHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, storage);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new AddRecipeStepCommand(recipe.Id, "B1", "D1", ImageUrl: storage.GetPublicUrl("recipes/2026/11/cua-nguoi-khac.jpg")),
            CancellationToken.None));
    }

    [Fact]
    public async Task AddStep_WithImageAlreadyUsedAsRecipeImage_Conflicts()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var storage = new TestRecipeFileStorageService();
        await SeedUploadAsync(db, storage, "recipes/2026/11/bia.jpg", Jpeg, recipe.AuthorId);
        var url = storage.GetPublicUrl("recipes/2026/11/bia.jpg");
        db.RecipeImages.Add(new RecipeImage { Id = Guid.NewGuid(), RecipeId = recipe.Id, OriginalUrl = url, IsPrimary = true });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => CreateAddStepHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, storage)
            .Handle(new AddRecipeStepCommand(recipe.Id, "B1", "D1", ImageUrl: url), CancellationToken.None));
    }

    [Fact]
    public async Task AddRecipeImage_WithImageAlreadyUsedByStep_Conflicts()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var storage = new TestRecipeFileStorageService();
        await SeedUploadAsync(db, storage, "recipes/2026/11/buoc.jpg", Jpeg, recipe.AuthorId);
        var url = storage.GetPublicUrl("recipes/2026/11/buoc.jpg");
        db.RecipeSteps.Add(new RecipeStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 1, Title = "B1", Description = "D1", ImageUrl = url });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => CreateAddImageHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, storage)
            .Handle(new AddRecipeImageCommand(recipe.Id, url, null), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateStep_KeepingExistingImage_DoesNotRevalidateOrDelete()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        // Dữ liệu cũ (seed) dùng link ngoài — sửa chữ của bước không được bắt tải ảnh lại.
        var step = new RecipeStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 1, Title = "B1", Description = "D1", ImageUrl = "https://example.com/cu.jpg" };
        db.RecipeSteps.Add(step);
        await db.SaveChangesAsync();
        var scheduler = new TestFileDeletionScheduler();

        var result = await CreateUpdateStepHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, new TestRecipeFileStorageService(), scheduler)
            .Handle(new UpdateRecipeStepCommand(recipe.Id, step.Id, "B1 sửa", "D1", ImageUrl: "https://example.com/cu.jpg"), CancellationToken.None);

        Assert.Equal("B1 sửa", result.Title);
        Assert.Equal("https://example.com/cu.jpg", result.ImageUrl);
        Assert.Empty(scheduler.Scheduled);
    }

    [Fact]
    public async Task UpdateStep_ReplacingImage_SchedulesOldFileDeletion()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var storage = new TestRecipeFileStorageService();
        await SeedUploadAsync(db, storage, "recipes/2026/11/cu.jpg", Jpeg, recipe.AuthorId);
        await SeedUploadAsync(db, storage, "recipes/2026/11/moi.jpg", Jpeg, recipe.AuthorId);
        var step = new RecipeStep
        {
            Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 1, Title = "B1", Description = "D1",
            ImageUrl = storage.GetPublicUrl("recipes/2026/11/cu.jpg"),
        };
        db.RecipeSteps.Add(step);
        await db.SaveChangesAsync();
        var scheduler = new TestFileDeletionScheduler();

        var result = await CreateUpdateStepHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, storage, scheduler)
            .Handle(new UpdateRecipeStepCommand(recipe.Id, step.Id, "B1", "D1", ImageUrl: storage.GetPublicUrl("recipes/2026/11/moi.jpg")),
                CancellationToken.None);

        Assert.Equal(storage.GetPublicUrl("recipes/2026/11/moi.jpg"), result.ImageUrl);
        Assert.Equal(new[] { "recipes/2026/11/cu.jpg" }, scheduler.Scheduled);
    }

    [Fact]
    public async Task UpdateStep_RemovingImage_ClearsUrlAndSchedulesDeletion()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var storage = new TestRecipeFileStorageService();
        var step = new RecipeStep
        {
            Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 1, Title = "B1", Description = "D1",
            ImageUrl = storage.GetPublicUrl("recipes/2026/11/go.jpg"),
        };
        db.RecipeSteps.Add(step);
        await db.SaveChangesAsync();
        var scheduler = new TestFileDeletionScheduler();

        var result = await CreateUpdateStepHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, storage, scheduler)
            .Handle(new UpdateRecipeStepCommand(recipe.Id, step.Id, "B1", "D1", ImageUrl: null), CancellationToken.None);

        Assert.Null(result.ImageUrl);
        Assert.Equal(new[] { "recipes/2026/11/go.jpg" }, scheduler.Scheduled);
    }

    [Fact]
    public async Task DeleteStep_WithImage_SchedulesFileDeletion()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var user = new TestCurrentUser { UserId = recipe.AuthorId };
        var storage = new TestRecipeFileStorageService();
        var step = new RecipeStep
        {
            Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = 1, Title = "B1", Description = "D1",
            ImageUrl = storage.GetPublicUrl("recipes/2026/11/xoa.jpg"),
        };
        db.RecipeSteps.Add(step);
        await db.SaveChangesAsync();
        var scheduler = new TestFileDeletionScheduler();

        await new DeleteRecipeStepCommandHandler(
                new RepositoryBase<Recipe>(db), new RepositoryBase<RecipeStep>(db), db, user,
                imageGuard: CreateImageGuard(db, user, storage), fileDeletionScheduler: scheduler)
            .Handle(new DeleteRecipeStepCommand(recipe.Id, step.Id), CancellationToken.None);

        Assert.Equal(new[] { "recipes/2026/11/xoa.jpg" }, scheduler.Scheduled);
    }

    [Fact]
    public async Task AddStep_WithImageAlreadyUsedByCategory_Conflicts()
    {
        using var db = CreateDbContext();
        var recipe = await SeedRecipeAsync(db);
        var storage = new TestRecipeFileStorageService();
        await SeedUploadAsync(db, storage, "categories/mon-nuoc.jpg", Jpeg, recipe.AuthorId);
        var url = storage.GetPublicUrl("categories/mon-nuoc.jpg");
        db.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Món nước", Slug = "mon-nuoc-2", ImageUrl = url });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => CreateAddStepHandler(db, new TestCurrentUser { UserId = recipe.AuthorId }, storage)
            .Handle(new AddRecipeStepCommand(recipe.Id, "B1", "D1", ImageUrl: url), CancellationToken.None));
    }
}
