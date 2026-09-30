using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Application.Recipes.Dtos;

public record RecipeDto(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    int PrepTime,
    int CookTime,
    int Servings,
    RecipeDifficulty Difficulty,
    RecipeStatus Status,
    DateTimeOffset? PublishedAt);