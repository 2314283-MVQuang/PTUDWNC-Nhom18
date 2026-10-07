namespace CulinaryBlog.Application.Features.Recipes.Dtos;

public record RecipeIngredientDto(
    Guid Id,
    Guid RecipeId,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int OrderIndex);

public record RecipeStepDto(
    Guid Id,
    Guid RecipeId,
    int StepNumber,
    string Title,
    string Description,
    int? TimerMinutes,
    string? ImageUrl);

public record RecipeImageDto(
    Guid Id,
    Guid RecipeId,
    string OriginalUrl,
    string? MediumUrl,
    string? ThumbnailUrl,
    string? AltText,
    bool IsPrimary,
    int OrderIndex);
