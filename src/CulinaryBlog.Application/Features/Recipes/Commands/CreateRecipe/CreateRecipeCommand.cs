using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Enums;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;

/// <summary>
/// FR-RCP-003 - tạo Recipe.
/// AuthorId không nhận từ client.
/// Handler lấy AuthorId từ JWT qua ICurrentUser.
/// </summary>
public sealed record CreateRecipeCommand(
    string Title,
    string Description,
    Guid CategoryId,
    int PrepTime,
    int CookTime,
    int Servings,
    RecipeDifficulty Difficulty,
    string Instructions,
    RecipeNutritionInput? Nutrition,
    CreateRecipeStepInput[]? Steps,
    CreateRecipeIngredientInput[]? Ingredients)
    : IRequest<RecipeDetailDto>;

public sealed record RecipeNutritionInput(
    decimal? Calories,
    decimal? Protein,
    decimal? Carbohydrates,
    decimal? Fat,
    decimal? Fiber,
    decimal? Sodium);

public sealed record CreateRecipeStepInput(
    int StepNumber,
    string Title,
    string Description,
    int? TimerMinutes,
    string? ImageUrl = null);

public sealed record CreateRecipeIngredientInput(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int OrderIndex);