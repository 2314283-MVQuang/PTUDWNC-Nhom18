using FluentValidation;

namespace CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;

public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID danh mục không được để trống.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MinimumLength(2).WithMessage("Tên danh mục phải có ít nhất 2 ký tự.")
            .MaximumLength(100).WithMessage("Tên danh mục không được vượt quá 100 ký tự.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Mô tả không được vượt quá 2000 ký tự.")
            .When(x => x.Description is not null);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500).WithMessage("Đường dẫn ảnh không được vượt quá 500 ký tự.")
            .When(x => x.ImageUrl is not null);
    }
}
