using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Features.Users;

public sealed record UserListDto(string Id, string Email, string DisplayName, string? AvatarUrl,
    bool IsActive, DateTimeOffset CreatedAt, IReadOnlyList<string> Roles);

public interface IUserDirectory
{
    Task<PagedResult<UserListDto>> GetAsync(GetUsersQuery query, CancellationToken ct);
}

public sealed record GetUsersQuery(int Page = 1, int PageSize = 12, string? Role = null, string? Q = null)
    : IRequest<PagedResult<UserListDto>>;

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Role).Must(role => role is null or "" or "Admin" or "Author").WithMessage("Vai trò phải là Admin hoặc Author.");
        RuleFor(x => x.Q).MaximumLength(100);
    }
}

public sealed class GetUsersQueryHandler(IUserDirectory directory, ICurrentUser user)
    : IRequestHandler<GetUsersQuery, PagedResult<UserListDto>>
{
    public Task<PagedResult<UserListDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        if (!user.IsInRole("Admin")) throw new ForbiddenAccessException("Chỉ quản trị viên được xem danh sách người dùng.");
        return directory.GetAsync(request, ct);
    }
}
