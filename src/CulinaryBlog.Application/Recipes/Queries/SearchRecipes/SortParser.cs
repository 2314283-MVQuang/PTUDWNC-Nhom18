using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

public sealed record ParsedRecipeSort(string Field, bool IsDescending);

public static class SortParser
{
    private static readonly string[] AllowedFields =
    [
        "createdAt",
        "publishedAt",
        "title",
        "prepTime",
        "cookTime"
    ];

    private static readonly HashSet<string> AllowedFieldSet =
        new(AllowedFields, StringComparer.OrdinalIgnoreCase);

    public static ParsedRecipeSort? Parse(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return null;
        }

        var value = sort.Trim();
        var isDescending = value.StartsWith("-", StringComparison.Ordinal);
        var field = isDescending ? value[1..] : value;

        if (!AllowedFieldSet.Contains(field))
        {
            throw new InvalidSortException(field, AllowedFields);
        }

        return new ParsedRecipeSort(field.ToLowerInvariant(), isDescending);
    }

    public static IEnumerable<Recipe> Apply(
        IEnumerable<Recipe> recipes,
        ParsedRecipeSort? sort)
    {
        if (sort is null)
        {
            return recipes;
        }

        return (sort.Field, sort.IsDescending) switch
        {
            ("createdat", false) => recipes.OrderBy(recipe => recipe.CreatedAt),
            ("createdat", true) => recipes.OrderByDescending(recipe => recipe.CreatedAt),
            ("publishedat", false) => recipes.OrderBy(recipe => recipe.PublishedAt),
            ("publishedat", true) => recipes.OrderByDescending(recipe => recipe.PublishedAt),
            ("title", false) =>
                recipes.OrderBy(recipe => recipe.Title, StringComparer.OrdinalIgnoreCase),
            ("title", true) =>
                recipes.OrderByDescending(recipe => recipe.Title, StringComparer.OrdinalIgnoreCase),
            ("preptime", false) => recipes.OrderBy(recipe => recipe.PrepTime),
            ("preptime", true) => recipes.OrderByDescending(recipe => recipe.PrepTime),
            ("cooktime", false) => recipes.OrderBy(recipe => recipe.CookTime),
            ("cooktime", true) => recipes.OrderByDescending(recipe => recipe.CookTime),
            _ => throw new InvalidOperationException($"Unsupported parsed sort field '{sort.Field}'.")
        };
    }
}
