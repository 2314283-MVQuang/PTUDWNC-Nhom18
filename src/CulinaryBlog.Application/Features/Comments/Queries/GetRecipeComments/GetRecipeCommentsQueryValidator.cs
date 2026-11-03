using FluentValidation;

namespace CulinaryBlog.Application.Features.Comments.Queries.GetRecipeComments;

public sealed class GetRecipeCommentsQueryValidator : AbstractValidator<GetRecipeCommentsQuery>
{
    public GetRecipeCommentsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page phải >= 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, GetRecipeCommentsQuery.MaxPageSize)
            .WithMessage($"PageSize phải nằm trong khoảng 1-{GetRecipeCommentsQuery.MaxPageSize}.");
    }
}
