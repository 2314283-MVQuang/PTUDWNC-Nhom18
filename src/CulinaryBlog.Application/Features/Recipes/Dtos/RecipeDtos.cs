using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Application.Features.Recipes.Dtos;

/// <summary>
/// DTO gọn cho GET /recipes.
/// Dùng cho RecipeCard / danh sách.
/// </summary>
public sealed record RecipeSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    int PrepTime,
    int CookTime,
    int Servings,
    RecipeDifficulty Difficulty,
    RecipeStatus Status,
    string CategoryName,
    string AuthorDisplayName,
    string? PrimaryImageUrl,
    DateTimeOffset? PublishedAt);

/// <summary>
/// DTO đầy đủ cho GET detail.
/// </summary>
public sealed record RecipeDetailDto(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    string Instructions,
    int PrepTime,
    int CookTime,
    int Servings,
    RecipeDifficulty Difficulty,
    RecipeStatus Status,
    Guid CategoryId,
    string CategoryName,
    string AuthorId,
    string AuthorDisplayName,
    DateTimeOffset? PublishedAt,
    RecipeNutritionDto Nutrition,
    IReadOnlyList<RecipeStepDto> Steps,
    IReadOnlyList<RecipeIngredientDto> Ingredients,
    IReadOnlyList<RecipeImageDto> Images,
    string RowVersion);

public sealed record RecipeNutritionDto(
    decimal? Calories,
    decimal? Protein,
    decimal? Carbohydrates,
    decimal? Fat,
    decimal? Fiber,
    decimal? Sodium);

public sealed record RecipeStepDto(
    Guid Id,
    int StepNumber,
    string Title,
    string Description,
    int? TimerMinutes,
    string? ImageUrl);

public sealed record RecipeIngredientDto(
    Guid Id,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int OrderIndex);

public sealed record RecipeImageDto(
    Guid Id,
    string OriginalUrl,
    string? MediumUrl,
    string? ThumbnailUrl,
    string? AltText,
    bool IsPrimary,
    int OrderIndex);

/// <summary>
/// Mapping từ Entity sang DTO.
/// Không trả trực tiếp EF Entity ra ngoài API.
/// </summary>
public static class RecipeDtoMapping
{
    public static RecipeSummaryDto ToSummaryDto(this Recipe recipe) =>
        new(
            recipe.Id,
            recipe.Slug,
            recipe.Title,
            recipe.Description,
            recipe.PrepTime,
            recipe.CookTime,
            recipe.Servings,
            recipe.Difficulty,
            recipe.Status,
            recipe.Category.Name,
            recipe.Author.DisplayName,
            recipe.Images
                .OrderBy(x => x.OrderIndex)
                .Select(x => x.OriginalUrl)
                .FirstOrDefault(),
            recipe.PublishedAt);

    public static RecipeDetailDto ToDetailDto(this Recipe recipe) =>
        new(
            recipe.Id,
            recipe.Slug,
            recipe.Title,
            recipe.Description,
            recipe.Instructions,
            recipe.PrepTime,
            recipe.CookTime,
            recipe.Servings,
            recipe.Difficulty,
            recipe.Status,
            recipe.CategoryId,
            recipe.Category.Name,
            recipe.AuthorId,
            recipe.Author.DisplayName,
            recipe.PublishedAt,

            new RecipeNutritionDto(
                recipe.Nutrition.Calories,
                recipe.Nutrition.Protein,
                recipe.Nutrition.Carbohydrates,
                recipe.Nutrition.Fat,
                recipe.Nutrition.Fiber,
                recipe.Nutrition.Sodium),

            recipe.Steps
                .OrderBy(x => x.StepNumber)
                .Select(x => new RecipeStepDto(
                    x.Id,
                    x.StepNumber,
                    x.Title,
                    x.Description,
                    x.TimerMinutes,
                    x.ImageUrl))
                .ToList(),

            recipe.Ingredients
                .OrderBy(x => x.OrderIndex)
                .Select(x => new RecipeIngredientDto(
                    x.Id,
                    x.Name,
                    x.Quantity,
                    x.Unit,
                    x.Notes,
                    x.OrderIndex))
                .ToList(),

            recipe.Images
                .OrderBy(x => x.OrderIndex)
                .Select(x => new RecipeImageDto(
                    x.Id,
                    x.OriginalUrl,
                    x.MediumUrl,
                    x.ThumbnailUrl,
                    x.AltText,
                    x.IsPrimary,
                    x.OrderIndex))
                .ToList(),

            Convert.ToBase64String(recipe.RowVersion));
}