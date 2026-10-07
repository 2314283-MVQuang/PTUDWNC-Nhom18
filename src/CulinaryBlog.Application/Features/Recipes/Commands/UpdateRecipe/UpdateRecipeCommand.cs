using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Enums;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UpdateRecipe;

/// <summary>
/// FR-RCP-004.
/// IfMatch lấy từ HTTP Header "If-Match", không nằm trong JSON body.
/// </summary>
public sealed record UpdateRecipeCommand(
    Guid Id,
    string? Title,
    string? Description,
    Guid? CategoryId,
    int? PrepTime,
    int? CookTime,
    int? Servings,
    RecipeDifficulty? Difficulty,
    string? Instructions,
    UpdateRecipeNutritionInput? Nutrition,
    string IfMatch)
    : IRequest<RecipeDetailDto>;

public sealed record UpdateRecipeNutritionInput(
    decimal? Calories,
    decimal? Protein,
    decimal? Carbohydrates,
    decimal? Fat,
    decimal? Fiber,
    decimal? Sodium);