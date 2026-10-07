namespace CulinaryBlog.Application.Features.Recipes.Dtos;

public record RecipeIngredientItemDto(
    Guid Id,
    Guid RecipeId,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int OrderIndex);

public record RecipeStepItemDto(
    Guid Id,
    Guid RecipeId,
    int StepNumber,
    string Title,
    string Description,
    int? TimerMinutes,
    string? ImageUrl);

public record RecipeImageItemDto(
    Guid Id,
    Guid RecipeId,
    string OriginalUrl,
    string? MediumUrl,
    string? ThumbnailUrl,
    string? AltText,
    bool IsPrimary,
    int OrderIndex);
