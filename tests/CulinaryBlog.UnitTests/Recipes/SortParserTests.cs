using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.Queries.SearchRecipes;
using CulinaryBlog.Domain.Entities;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

public class SortParserTests
{
    [Theory]
    [InlineData("title", "title", false)]
    [InlineData("-createdAt", "createdat", true)]
    [InlineData(" PUBLISHEDAT ", "publishedat", false)]
    public void Parse_ShouldReturnWhitelistedFieldAndDirection(
        string input,
        string expectedField,
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
        Assert.Null(SortParser.Parse(input));
    }

    [Fact]
    public void Parse_ShouldRejectFieldOutsideWhitelist()
    {
        var exception = Assert.Throws<InvalidSortException>(() => SortParser.Parse("authorId"));

        Assert.Contains("authorId", exception.Message);
        Assert.Contains("createdAt", exception.Message);
    }

    [Fact]
    public void Apply_ShouldPreserveInputOrder_WhenSortIsMissing()
    {
        var recipes = new[]
        {
            new Recipe { Title = "First" },
            new Recipe { Title = "Second" }
        };

        Assert.Equal(recipes, SortParser.Apply(recipes, null));
    }

    [Theory]
    [InlineData("title", "A", "B")]
    [InlineData("-title", "B", "A")]
    public void Apply_ShouldSortInRequestedDirection(
        string sortField,
        string firstExpected,
        string secondExpected)
    {
        var recipes = new[]
        {
            new Recipe { Title = "B" },
            new Recipe { Title = "A" }
        };

        var sorted = SortParser.Apply(recipes, SortParser.Parse(sortField));

        Assert.Equal(new[] { firstExpected, secondExpected }, sorted.Select(recipe => recipe.Title));
    }
}
