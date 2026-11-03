using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.UnitTests.Categories;

public class TestCacheInvalidator : ICacheInvalidator
{
    public List<string> EvictedTags { get; } = [];

    public Task EvictByTagAsync(string tag, CancellationToken ct = default)
    {
        EvictedTags.Add(tag);
        return Task.CompletedTask;
    }
}

public class UpdateCategoryCommandHandlerTests
{
    private CulinaryBlogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CulinaryBlogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CulinaryBlogDbContext(options);
    }

    [Fact]
    public async Task Handle_ShouldUpdateNameButKeepSlug_WhenRenamed()
    {
        // Arrange
        using var db = CreateDbContext();
        var catRepo = new RepositoryBase<Category>(db);
        var cacheInv = new TestCacheInvalidator();

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Món Khai Vị",
            Slug = "mon-khai-vi",
            Description = "Mô tả cũ",
            ImageUrl = "https://example.com/old.jpg"
        };
        await db.Categories.AddAsync(category);
        await db.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(catRepo, db, cacheInv);
        var command = new UpdateCategoryCommand(
            category.Id,
            "Món Khai Vị Đặc Biệt",
            "Mô tả mới",
            "https://example.com/new.jpg");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Món Khai Vị Đặc Biệt", result.Name);
        Assert.Equal("mon-khai-vi", result.Slug); // SRS FR-CAT-004: slug không đổi khi đổi tên
        Assert.Equal("Mô tả mới", result.Description);
        Assert.Equal("https://example.com/new.jpg", result.ImageUrl);
        Assert.Contains("categories", cacheInv.EvictedTags);
    }

    [Fact]
    public async Task Handle_ShouldNotTouchSlug_WhenNewNameWouldCollideWithAnotherSlug()
    {
        // Arrange: Đã tồn tại Category 1 tên "Mon Chay" (không dấu) có slug "mon-chay"
        using var db = CreateDbContext();
        var catRepo = new RepositoryBase<Category>(db);
        var cacheInv = new TestCacheInvalidator();

        var existingCategory = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Mon Chay",
            Slug = "mon-chay",
        };
        var targetCategory = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Món Mặn",
            Slug = "mon-man",
        };
        await db.Categories.AddRangeAsync(existingCategory, targetCategory);
        await db.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(catRepo, db, cacheInv);

        // Act: Đổi tên targetCategory thành "Món Chay" — TÊN khác "Mon Chay" nhưng SLUG trùng "mon-chay"
        var command = new UpdateCategoryCommand(targetCategory.Id, "Món Chay");
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: đổi tên thành công, slug giữ nguyên nên không có xung đột slug nào (FR-CAT-004)
        Assert.NotNull(result);
        Assert.Equal("Món Chay", result.Name);
        Assert.Equal("mon-man", result.Slug);
        Assert.Contains("categories", cacheInv.EvictedTags);
    }

    [Fact]
    public async Task Handle_ShouldKeepOriginalSlug_WhenRenamedMultipleTimes()
    {
        // Arrange: Đã tồn tại "mon-trang-mieng" và "mon-trang-mieng-2"
        using var db = CreateDbContext();
        var catRepo = new RepositoryBase<Category>(db);

        var cat1 = new Category { Id = Guid.NewGuid(), Name = "Mon Trang Mieng", Slug = "mon-trang-mieng" };
        var cat2 = new Category { Id = Guid.NewGuid(), Name = "Món Tráng Miệng 2", Slug = "mon-trang-mieng-2" };
        var catToUpdate = new Category { Id = Guid.NewGuid(), Name = "Đồ Uống", Slug = "do-uong" };

        await db.Categories.AddRangeAsync(cat1, cat2, catToUpdate);
        await db.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(catRepo, db);

        // Act: Đổi tên catToUpdate thành "Món Tráng Miệng"
        var command = new UpdateCategoryCommand(catToUpdate.Id, "Món Tráng Miệng");
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: slug vẫn là slug gốc
        Assert.Equal("do-uong", result.Slug);
        var again = await handler.Handle(new UpdateCategoryCommand(catToUpdate.Id, "Tráng Miệng Mùa Hè"), CancellationToken.None);
        Assert.Equal("do-uong", again.Slug);
    }

    [Fact]
    public async Task Handle_ShouldKeepCleanSlug_WhenNameIsIdenticalToOwnCurrentSlug()
    {
        // Arrange: Category cập nhật mô tả nhưng tên giữ nguyên
        using var db = CreateDbContext();
        var catRepo = new RepositoryBase<Category>(db);

        var category = new Category { Id = Guid.NewGuid(), Name = "Hải Sản", Slug = "hai-san" };
        await db.Categories.AddAsync(category);
        await db.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(catRepo, db);

        // Act
        var command = new UpdateCategoryCommand(category.Id, "Hải Sản", "Mô tả mới");
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: Không bị thêm hậu tố -2 nhầm vì chính mình đã có slug đó
        Assert.Equal("hai-san", result.Slug);
    }

    [Fact]
    public async Task Handle_ShouldThrowConflictException_WhenNameAlreadyUsedByAnotherCategory()
    {
        // Arrange: tên danh mục là duy nhất (SRS FR-CAT-003) — đổi sang tên đã có phải trả 409
        using var db = CreateDbContext();
        var catRepo = new RepositoryBase<Category>(db);

        var existing = new Category { Id = Guid.NewGuid(), Name = "Món Chay", Slug = "mon-chay" };
        var target = new Category { Id = Guid.NewGuid(), Name = "Món Mặn", Slug = "mon-man" };
        await db.Categories.AddRangeAsync(existing, target);
        await db.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(catRepo, db);
        var command = new UpdateCategoryCommand(target.Id, "Món Chay");

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange
        using var db = CreateDbContext();
        var catRepo = new RepositoryBase<Category>(db);
        var handler = new UpdateCategoryCommandHandler(catRepo, db);

        var command = new UpdateCategoryCommand(Guid.NewGuid(), "Tên Bất Kỳ");

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }
}
