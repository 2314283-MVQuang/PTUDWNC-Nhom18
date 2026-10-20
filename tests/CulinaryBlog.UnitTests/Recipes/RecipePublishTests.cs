using CulinaryBlog.Domain.Common.Exceptions;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

public class RecipePublishTests
{
    private static Recipe CreateRecipe()
    {
       return new Recipe
       {
           Title = "Phở bò",
           Slug = "pho-bo",
           Description = "Món ăn truyền thống Việt Nam",
           Instructions = "Nấu nước dùng, cho bánh phở và thịt bò vào, thêm rau thơm.",
           PrepTime =20,
           CookTime = 60,
           Servings = 4,
           CategoryId = Guid.NewGuid(),
           AuthorId = Guid.NewGuid().ToString()
       } ;
    }

    [Fact]
    public void Publish_ShouldFail_WhenNoStepsAndNoIngredients()
    {
        // Arrange
        var recipe = CreateRecipe();

        // Act & Assert
        var exception = Assert.Throws<RecipePublishIncompleteException>(
            () => recipe.Publish());

        Assert.Contains("ít nhất 1 bước", exception.Message);
        Assert.Equal(RecipeStatus.Draft, recipe.Status);
    }

    [Fact]
    public void Publish_ShouldFail_WhenNoSteps()
    {
        // Arrange
        var recipe = CreateRecipe();
        recipe.Ingredients.Add(new RecipeIngredient { Name = "Thịt bò", OrderIndex = 0});

        // Act & Assert
        var exception = Assert.Throws<RecipePublishIncompleteException>(
            () => recipe.Publish());

        Assert.Contains("ít nhất 1 bước thực hiện", exception.Message);
        Assert.Equal(RecipeStatus.Draft, recipe.Status);
    }

    [Fact]
    public void Publish_ShouldFail_WhenNoIngredients()
    {
        // Arrange
        var recipe = CreateRecipe();
        recipe.Steps.Add(new RecipeStep { StepNumber = 1, Title = "Bước 1", Description = "Nấu nước dùng" });

        // Act & Assert
        var exception = Assert.Throws<RecipePublishIncompleteException>(
            () => recipe.Publish());

        Assert.Contains("ít nhất 1 nguyên liệu", exception.Message);
        Assert.Equal(RecipeStatus.Draft, recipe.Status);
    }

    [Fact]
    public void Publish_ShouldSetPublishedStatus_WhenComplete()
    {
        // Arrange
        var recipe = CreateRecipe();
        recipe.Steps.Add(new RecipeStep { StepNumber = 1, Title = "Bước 1", Description = "Nấu nước dùng" });
        recipe.Ingredients.Add(new RecipeIngredient { Name = "Thịt bò", OrderIndex = 0});

        // Act
        recipe.Publish();

        // Assert
        Assert.Equal(RecipeStatus.Published, recipe.Status);

        Assert.NotNull(recipe.PublishedAt);
    }

    [Fact]
    public void Publish_ShouldBeIdempotent()
    {
        // Arrange
        var recipe = CreateRecipe();
        recipe.Steps.Add(new RecipeStep { StepNumber = 1, Title = "Bước 1", Description = "Nấu nước dùng" });

        recipe.Ingredients.Add(new RecipeIngredient { Name = "Thịt bò", OrderIndex = 0});

        // Act
        recipe.Publish();
        var firstPublishedAt = recipe.PublishedAt;

        // Act again
        recipe.Publish();

        // Assert
        Assert.Equal(RecipeStatus.Published, recipe.Status);
        Assert.Equal(firstPublishedAt, recipe.PublishedAt);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Publish_ShouldRejectSoftDeletedParts(bool stepDeleted, bool ingredientDeleted)
    {
        var recipe = CreateRecipe();
        recipe.Steps.Add(new RecipeStep { IsDeleted = stepDeleted });
        recipe.Ingredients.Add(new RecipeIngredient { IsDeleted = ingredientDeleted });

        Assert.Throws<RecipePublishIncompleteException>(() => recipe.Publish());
        Assert.Equal(RecipeStatus.Draft, recipe.Status);
        Assert.Null(recipe.PublishedAt);
    }
}
