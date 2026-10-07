using FluentValidation;

namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MinimumLength(2).WithMessage("Tên danh mục phải có ít nhất 2 ký tự.")
            .MaximumLength(50).WithMessage("Tên danh mục không được vượt quá 50 ký tự.")
            .Must(name => name is null || (!name.Contains('<') && !name.Contains('>')))
            .WithMessage("Tên danh mục không được chứa thẻ HTML.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Mô tả không được vượt quá 2000 ký tự.")
            .When(x => x.Description is not null);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500).WithMessage("Đường dẫn ảnh không được vượt quá 500 ký tự.")
            .When(x => x.ImageUrl is not null);
    }
}