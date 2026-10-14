using FluentValidation;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;

public sealed class GetCategoryBySlugQueryValidator : AbstractValidator<GetCategoryBySlugQuery>
{
    public GetCategoryBySlugQueryValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug danh mục không được để trống.")
            .MaximumLength(120).WithMessage("Slug danh mục không được vượt quá 120 ký tự.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page phải >= 1.");

        // Giới hạn pageSize tối đa giống GET /recipes — chặn một request kéo cả bảng (DoS đơn giản).
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("PageSize phải nằm trong khoảng 1-50.");
    }
}
