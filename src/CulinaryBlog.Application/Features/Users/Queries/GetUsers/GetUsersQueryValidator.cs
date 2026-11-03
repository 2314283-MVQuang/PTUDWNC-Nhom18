using FluentValidation;

namespace CulinaryBlog.Application.Features.Users.Queries.GetUsers;

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page phải >= 1.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, GetUsersQuery.MaxPageSize)
            .WithMessage($"PageSize phải nằm trong khoảng 1-{GetUsersQuery.MaxPageSize}.");
        RuleFor(x => x.Role)
            .Must(role => role is null or GetUsersQuery.AdminRole or GetUsersQuery.AuthorRole)
            .WithMessage("Role chỉ nhận Admin hoặc Author.");
        RuleFor(x => x.Keyword).MaximumLength(100);
    }
}
