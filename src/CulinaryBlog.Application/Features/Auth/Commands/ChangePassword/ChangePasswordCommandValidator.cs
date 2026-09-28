using FluentValidation;

namespace CulinaryBlog.Application.Features.Auth.Commands.ChangePassword;

/// <summary>Rule mật khẩu mới giống hệt RegisterCommandValidator (mục 5.2) — không nới lỏng chuẩn.</summary>
public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu mới phải có ít nhất 1 chữ hoa.")
            .Matches("[0-9]").WithMessage("Mật khẩu mới phải có ít nhất 1 chữ số.")
            .Matches(@"[!@#$%^&*(),.?"":{}|<>_\-+=\[\]/\\;'~`]").WithMessage("Mật khẩu mới phải có ít nhất 1 ký tự đặc biệt.")
            .NotEqual(x => x.CurrentPassword).WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");
    }
}
