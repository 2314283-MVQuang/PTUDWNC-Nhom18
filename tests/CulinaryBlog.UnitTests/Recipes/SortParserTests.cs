using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.Queries.SearchRecipes;
using CulinaryBlog.Domain.Interfaces;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-SRCH-003 — đọc tham số sort của /recipes/search.</summary>
public class SortParserTests
{
    [Theory]
    [InlineData("title", RecipeSearchSortField.Title, false)]
    [InlineData("-createdAt", RecipeSearchSortField.CreatedAt, true)]
    [InlineData(" PUBLISHEDAT ", RecipeSearchSortField.PublishedAt, false)]
    [InlineData("-cookTime", RecipeSearchSortField.CookTime, true)]
    [InlineData("prepTime", RecipeSearchSortField.PrepTime, false)]
    public void Parse_ShouldReturnWhitelistedFieldAndDirection(
        string input,
        RecipeSearchSortField expectedField,
        bool expectedDescending)
    {
        var sort = SortParser.Parse(input);

        Assert.NotNull(sort);
        Assert.Equal(expectedField, sort.Field);
        Assert.Equal(expectedDescending, sort.IsDescending);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Parse_ShouldReturnNull_WhenSortIsMissing(string? input)
    {
        // null = giữ thứ tự theo độ liên quan (ts_rank) của FR-SRCH-001.
        Assert.Null(SortParser.Parse(input));
    }

    [Theory]
    [InlineData("authorId")]
    [InlineData("-")]
    [InlineData("--title")]
    [InlineData("title;DROP TABLE \"Recipes\"")]
    public void Parse_ShouldRejectFieldOutsideWhitelist(string input)
    {
        var exception = Assert.Throws<InvalidSortException>(() => SortParser.Parse(input));

        Assert.Contains("createdAt", exception.Message);
    }

    [Fact]
    public void Parse_ShouldMentionTheRejectedField()
    {
        var exception = Assert.Throws<InvalidSortException>(() => SortParser.Parse("authorId"));

        Assert.Contains("authorId", exception.Message);
    }
}
