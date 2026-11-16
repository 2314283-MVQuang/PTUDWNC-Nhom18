using CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;
using CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe;
using CulinaryBlog.Domain.Enums;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>
/// Dinh dưỡng cho 1 khẩu phần (RecipeNutrition, SRS mục 7.2.1) — trường tuỳ chọn của
/// FR-RCP-003/004: bỏ trống được, nếu có thì phải từ 0 đến 99 999.
/// </summary>
public class RecipeNutritionValidatorTests
{
    private static CreateRecipeCommand Create(RecipeNutritionInput? nutrition) => new(
        "Gà luộc lá chanh", "Gà ta luộc chấm muối tiêu chanh", Guid.NewGuid(),
        PrepTime: 15, CookTime: 30, Servings: 4, RecipeDifficulty.Easy,
        Instructions: null, nutrition, Steps: null, Ingredients: null);

    private static UpdateRecipeCommand Update(UpdateRecipeNutritionInput? nutrition) => new(
        Guid.NewGuid(), Title: null, Description: null, CategoryId: null, PrepTime: null, CookTime: null,
        Servings: null, Difficulty: null, Instructions: null, nutrition, IfMatch: "AAAAAAAAB9E=");

    [Fact]
    public void Create_WithoutNutrition_IsValid()
    {
        var result = new CreateRecipeCommandValidator().Validate(Create(null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Create_WithPartialNutrition_IsValid()
    {
        var nutrition = new RecipeNutritionInput(Calories: 350, Protein: 28.5m, Carbohydrates: null, Fat: 0, Fiber: null, Sodium: 99999);

        var result = new CreateRecipeCommandValidator().Validate(Create(nutrition));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100000)]
    public void Create_WithOutOfRangeCalories_Fails(double calories)
    {
        var nutrition = new RecipeNutritionInput((decimal)calories, null, null, null, null, null);

        var result = new CreateRecipeCommandValidator().Validate(Create(nutrition));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Nutrition.Calories");
    }

    [Fact]
    public void Update_WithAllNullNutrition_IsValid_SoClientCanClearValues()
    {
        var nutrition = new UpdateRecipeNutritionInput(null, null, null, null, null, null);

        var result = new UpdateRecipeCommandValidator().Validate(Update(nutrition));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Update_WithNegativeSodium_Fails()
    {
        var nutrition = new UpdateRecipeNutritionInput(200, null, null, null, null, -5);

        var result = new UpdateRecipeCommandValidator().Validate(Update(nutrition));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Nutrition.Sodium");
    }
}
