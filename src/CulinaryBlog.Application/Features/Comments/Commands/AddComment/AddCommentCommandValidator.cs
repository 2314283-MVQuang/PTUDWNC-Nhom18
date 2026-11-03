using CulinaryBlog.Domain.Entities;
using FluentValidation;

namespace CulinaryBlog.Application.Features.Comments.Commands.AddComment;

public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.Content)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Nội dung bình luận không được để trống.")
            .Must(c => (c?.Trim().Length ?? 0) <= RecipeComment.MaxContentLength)
            .WithMessage($"Bình luận tối đa {RecipeComment.MaxContentLength} ký tự.");
    }
}
