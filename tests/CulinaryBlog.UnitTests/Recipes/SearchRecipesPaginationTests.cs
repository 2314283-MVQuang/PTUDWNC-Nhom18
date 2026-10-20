using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Recipes.Queries.SearchRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

public sealed class SearchRecipesPaginationTests
{
    [Fact]
    public async Task SearchRecipesPaged_EmptyKeyword_ReturnsEmptyListAndZeroTotalCount()
    {
        await using var fixture = new RecipeTestFixture();
        await fixture.SeedAsync(status: RecipeStatus.Published);

        var handler = new SearchRecipesQueryHandler(fixture.Recipes);
        var query = new SearchRecipesQuery(Keyword: "   ", Page: 1, PageSize: 10);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task SearchRecipesPaged_DraftRecipes_NotIncludedInResults()
    {
        await using var fixture = new RecipeTestFixture();
        // Seed 1 draft recipe có từ khoá "Phở"
        await fixture.SeedAsync(status: RecipeStatus.Draft);

        var handler = new SearchRecipesQueryHandler(fixture.Recipes);
        var query = new SearchRecipesQuery(Keyword: "Phở", Page: 1, PageSize: 10);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task SearchRecipesPaged_Pagination_CalculatesMetadataCorrectly()
    {
        await using var fixture = new RecipeTestFixture();
        var category = new Category { Name = "Món sợi", Slug = "mon-soi" };
        var author = new ApplicationUser { Id = "author-test", DisplayName = "Đầu bếp Test" };
        fixture.Db.Categories.Add(category);
        fixture.Db.Users.Add(author);

        for (int i = 1; i <= 5; i++)
        {
            fixture.Db.Recipes.Add(new Recipe
            {
                Title = $"Phở đặc biệt số {i}",
                Slug = $"pho-dac-biet-{i}",
                Description = "Món phở truyền thống thơm ngon",
                Instructions = "Nấu theo các bước",
                Category = category,
                Author = author,
                AuthorId = author.Id,
                Status = RecipeStatus.Published,
                PublishedAt = DateTimeOffset.UtcNow.AddMinutes(-i),
                PrepTime = 15,
                CookTime = 30 + i * 5,
                Servings = 4,
                RowVersion = [1, 2, 3, 4]
            });
        }
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var handler = new SearchRecipesQueryHandler(fixture.Recipes);

        // Trang 1: pageSize = 2
        var page1 = await handler.Handle(
            new SearchRecipesQuery(Keyword: "Phở", Page: 1, PageSize: 2),
            CancellationToken.None);

        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.PageSize);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);

        // Trang 2: pageSize = 2
        var page2 = await handler.Handle(
            new SearchRecipesQuery(Keyword: "Phở", Page: 2, PageSize: 2),
            CancellationToken.None);

        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(2, page2.Page);
        Assert.True(page2.HasNextPage);
        Assert.True(page2.HasPreviousPage);

        // Trang 3: pageSize = 2 (chỉ còn 1 item)
        var page3 = await handler.Handle(
            new SearchRecipesQuery(Keyword: "Phở", Page: 3, PageSize: 2),
            CancellationToken.None);

        Assert.Single(page3.Items);
        Assert.Equal(5, page3.TotalCount);
        Assert.Equal(3, page3.Page);
        Assert.False(page3.HasNextPage);
        Assert.True(page3.HasPreviousPage);
    }

    [Fact]
    public async Task SearchRecipesPaged_WithFilters_FiltersResultsCorrectly()
    {
        await using var fixture = new RecipeTestFixture();
        var cat1 = new Category { Name = "Món Bắc", Slug = "mon-bac" };
        var cat2 = new Category { Name = "Món Nam", Slug = "mon-nam" };
        var author = new ApplicationUser { Id = "author-filter", DisplayName = "Đầu bếp Filter" };
        fixture.Db.Categories.AddRange(cat1, cat2);
        fixture.Db.Users.Add(author);

        // Recipe 1: Cat1, Easy, CookTime 30, Servings 2
        fixture.Db.Recipes.Add(new Recipe
        {
            Title = "Bún chả Hà Nội",
            Slug = "bun-cha-ha-noi",
            Description = "Bún chả nướng than hoa",
            Category = cat1,
            Author = author,
            AuthorId = author.Id,
            Status = RecipeStatus.Published,
            Difficulty = RecipeDifficulty.Easy,
            CookTime = 30,
            Servings = 2,
            RowVersion = [1, 2]
        });

        // Recipe 2: Cat2, Hard, CookTime 90, Servings 6
        fixture.Db.Recipes.Add(new Recipe
        {
            Title = "Bún bò Huế cay nồng",
            Slug = "bun-bo-hue",
            Description = "Bún bò cay đậm đà",
            Category = cat2,
            Author = author,
            AuthorId = author.Id,
            Status = RecipeStatus.Published,
            Difficulty = RecipeDifficulty.Hard,
            CookTime = 90,
            Servings = 6,
            RowVersion = [1, 2]
        });

        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var handler = new SearchRecipesQueryHandler(fixture.Recipes);

        // Tìm từ khoá "Bún" nhưng lọc Category = cat1
        var resultCat1 = await handler.Handle(
            new SearchRecipesQuery(Keyword: "Bún", Page: 1, PageSize: 10, CategoryId: cat1.Id),
            CancellationToken.None);
        Assert.Single(resultCat1.Items);
        Assert.Equal("Bún chả Hà Nội", resultCat1.Items[0].Title);

        // Tìm từ khoá "Bún" nhưng lọc CookTime <= 45
        var resultCookTime = await handler.Handle(
            new SearchRecipesQuery(Keyword: "Bún", Page: 1, PageSize: 10, MaxCookTime: 45),
            CancellationToken.None);
        Assert.Single(resultCookTime.Items);
        Assert.Equal("Bún chả Hà Nội", resultCookTime.Items[0].Title);

        // Tìm từ khoá "Bún" nhưng lọc Difficulty = Hard
        var resultDifficulty = await handler.Handle(
            new SearchRecipesQuery(Keyword: "Bún", Page: 1, PageSize: 10, Difficulty: RecipeDifficulty.Hard),
            CancellationToken.None);
        Assert.Single(resultDifficulty.Items);
        Assert.Equal("Bún bò Huế cay nồng", resultDifficulty.Items[0].Title);
    }

    [Fact]
    public void SearchRecipesQueryValidator_ValidQuery_PassesValidation()
    {
        var validator = new SearchRecipesQueryValidator();
        var query = new SearchRecipesQuery(
            Keyword: "Phở",
            Page: 1,
            PageSize: 12,
            MaxCookTime: 60,
            MinServings: 2,
            Difficulty: RecipeDifficulty.Medium,
            Sort: "-createdAt");

        var result = validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SearchRecipesQueryValidator_InvalidPage_HasError(int invalidPage)
    {
        var validator = new SearchRecipesQueryValidator();
        var query = new SearchRecipesQuery(Page: invalidPage);

        var result = validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(51)]
    [InlineData(100)]
    public void SearchRecipesQueryValidator_PageSizeOutOfRange_HasError(int invalidPageSize)
    {
        var validator = new SearchRecipesQueryValidator();
        var query = new SearchRecipesQuery(PageSize: invalidPageSize);

        var result = validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void SearchRecipesQueryValidator_NegativeCookTime_HasError()
    {
        var validator = new SearchRecipesQueryValidator();
        var query = new SearchRecipesQuery(MaxCookTime: -10);

        var result = validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.MaxCookTime);
    }

    [Fact]
    public async Task SearchRecipesPaged_WithSort_SortsBeforePaging()
    {
        await using var fixture = new RecipeTestFixture();
        var category = new Category { Name = "Món sợi", Slug = "mon-soi-sort" };
        var author = new ApplicationUser { Id = "author-sort", DisplayName = "Đầu bếp Sort" };
        fixture.Db.Categories.Add(category);
        fixture.Db.Users.Add(author);
        foreach (var cookTime in new[] { 50, 10, 40, 20, 30 })
        {
            fixture.Db.Recipes.Add(new Recipe
            {
                Title = $"Phở nấu {cookTime} phút",
                Slug = $"pho-nau-{cookTime}",
                Description = "Món phở",
                Category = category,
                Author = author,
                AuthorId = author.Id,
                Status = RecipeStatus.Published,
                CookTime = cookTime,
                Servings = 2,
                RowVersion = [1, 2]
            });
        }
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = new SearchRecipesQueryHandler(fixture.Recipes);

        // Sắp xếp phải áp dụng cho TOÀN BỘ kết quả rồi mới cắt trang (FR-SRCH-003 + 004).
        var page1 = await handler.Handle(
            new SearchRecipesQuery(Keyword: "Phở", Sort: "cookTime", Page: 1, PageSize: 2), CancellationToken.None);
        var page2 = await handler.Handle(
            new SearchRecipesQuery(Keyword: "Phở", Sort: "cookTime", Page: 2, PageSize: 2), CancellationToken.None);
        var lastPage = await handler.Handle(
            new SearchRecipesQuery(Keyword: "Phở", Sort: "-cookTime", Page: 3, PageSize: 2), CancellationToken.None);

        Assert.Equal(new[] { 10, 20 }, page1.Items.Select(x => x.CookTime));
        Assert.Equal(new[] { 30, 40 }, page2.Items.Select(x => x.CookTime));
        Assert.Equal(new[] { 10 }, lastPage.Items.Select(x => x.CookTime));
    }

    [Fact]
    public async Task SearchRecipesPaged_PageBeyondLastPage_ReturnsEmptyItemsWithTotal()
    {
        await using var fixture = new RecipeTestFixture();
        await fixture.SeedAsync(status: RecipeStatus.Published); // 1 món "Phở bò"
        var handler = new SearchRecipesQueryHandler(fixture.Recipes);

        var result = await handler.Handle(
            new SearchRecipesQuery(Keyword: "Phở", Page: 5, PageSize: 10), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void SearchRecipesQuery_DefaultsMatchSrs()
    {
        // SRS FR-SRCH-004: page mặc định 1, pageSize mặc định 12, tối đa 50.
        var query = new SearchRecipesQuery("phở");

        Assert.Equal(1, query.Page);
        Assert.Equal(12, query.PageSize);
        Assert.Equal(50, SearchRecipesQuery.MaxPageSize);
    }
}
