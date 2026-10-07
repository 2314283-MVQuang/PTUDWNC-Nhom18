using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeBySlug;

public sealed class GetRecipeBySlugQueryValidator
    : AbstractValidator<GetRecipeBySlugQuery>
{
    public GetRecipeBySlugQueryValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithMessage("Slug không được để trống.")
            .MaximumLength(220)
            .WithMessage("Slug tối đa 220 ký tự.");
    }
}