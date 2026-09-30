using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipes;

public sealed class GetRecipesQueryValidator
    : AbstractValidator<GetRecipesQuery>
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

    public GetRecipesQueryValidator()
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
                "Hỗ trợ: createdAt, -createdAt, " +
                "title, -title, cookTime, -cookTime.");
    }
}