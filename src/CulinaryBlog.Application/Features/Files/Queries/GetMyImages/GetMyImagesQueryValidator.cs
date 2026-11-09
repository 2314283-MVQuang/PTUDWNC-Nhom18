using FluentValidation;

namespace CulinaryBlog.Application.Features.Files.Queries.GetMyImages;

public sealed class GetMyImagesQueryValidator : AbstractValidator<GetMyImagesQuery>
{
    public GetMyImagesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page phải >= 1.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, GetMyImagesQuery.MaxPageSize)
            .WithMessage($"PageSize phải nằm trong khoảng 1-{GetMyImagesQuery.MaxPageSize}.");
    }
}
