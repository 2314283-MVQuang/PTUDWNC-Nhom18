using FluentValidation;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

/// <summary>
/// Validator cho SearchRecipesQuery (FR-SRCH-004).
/// Giới hạn PageSize tối đa 50 để chặn DoS đơn giản.
/// </summary>
public sealed class SearchRecipesQueryValidator : AbstractValidator<SearchRecipesQuery>
{
    private static readonly HashSet<string> AllowedSorts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "createdAt",
            "-createdAt",
            "title",
            "-title",
            "cookTime",
            "-cookTime"
        };

    public SearchRecipesQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page phải >= 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("PageSize phải nằm trong khoảng 1-50.");

        RuleFor(x => x.MaxCookTime)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxCookTime.HasValue)
            .WithMessage("MaxCookTime không được âm.");

        RuleFor(x => x.MinServings)
            .GreaterThanOrEqualTo(1)
            .When(x => x.MinServings.HasValue)
            .WithMessage("MinServings phải >= 1.");

        RuleFor(x => x.Difficulty)
            .IsInEnum()
            .When(x => x.Difficulty.HasValue)
            .WithMessage("Difficulty không hợp lệ.");

        RuleFor(x => x.Sort)
            .Must(sort =>
                string.IsNullOrWhiteSpace(sort) ||
                AllowedSorts.Contains(sort))
            .WithMessage(
                "Sort không hợp lệ. " +
                "Hỗ trợ: createdAt, -createdAt, title, -title, cookTime, -cookTime.");
    }
}
