using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Users.Dtos;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Users.Commands.SetUserStatus;

public sealed class SetUserStatusCommandHandler(
    UserManager<ApplicationUser> userManager,
    IRepository<Recipe> recipes,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ILogger<SetUserStatusCommandHandler> logger)
    : IRequestHandler<SetUserStatusCommand, UserSummaryDto>
{
    public async Task<UserSummaryDto> Handle(SetUserStatusCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException(nameof(ApplicationUser), request.UserId);

        // Admin tự khoá chính mình thì có thể không còn ai mở khoá được.
        if (!request.IsActive && string.Equals(user.Id, currentUser.UserId, StringComparison.Ordinal))
        {
            throw new ForbiddenAccessException("Bạn không thể tự khoá tài khoản của chính mình.");
        }

        if (user.IsActive != request.IsActive)
        {
            user.IsActive = request.IsActive;
            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                throw new ConflictException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }

            if (!request.IsActive)
            {
                // Thu hồi mọi phiên đăng nhập: refresh token bị revoke, access token (15 phút) tự hết hạn.
                await refreshTokens.RevokeAllActiveTokensForUserAsync(user.Id, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }

            logger.LogInformation("Admin {AdminId} {Action} tài khoản {UserId} ({Email})",
                currentUser.UserId, request.IsActive ? "mở khoá" : "khoá", user.Id, user.Email);
        }

        var roles = (await userManager.GetRolesAsync(user)).OrderBy(r => r).ToList();
        var total = await recipes.Query().CountAsync(r => r.AuthorId == user.Id, ct);
        var published = await recipes.Query()
            .CountAsync(r => r.AuthorId == user.Id && r.Status == RecipeStatus.Published, ct);

        return new UserSummaryDto(user.Id, user.Email ?? string.Empty, user.UserName, user.DisplayName,
            user.AvatarUrl, roles, user.IsActive, user.EmailConfirmed, user.CreatedAt, total, published);
    }
}
