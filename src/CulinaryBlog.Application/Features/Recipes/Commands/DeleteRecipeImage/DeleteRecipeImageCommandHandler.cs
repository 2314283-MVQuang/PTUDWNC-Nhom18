using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipeImage;

public class DeleteRecipeImageCommandHandler(
    IRepository<Recipe> recipes,
    IRepository<RecipeImage> images,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICacheInvalidator? cacheInvalidator = null)
    : IRequestHandler<DeleteRecipeImageCommand>
{
    public async Task Handle(DeleteRecipeImageCommand request, CancellationToken ct)
    {
        var recipe = await recipes.Query()
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);

        if (recipe is null)
        {
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        if (currentUser.UserId != recipe.AuthorId && !currentUser.IsInRole("Admin"))
        {
            throw new ForbiddenAccessException("Bạn không có quyền xóa ảnh của công thức này.");
        }

        var image = await images.Query()
            .FirstOrDefaultAsync(i => i.Id == request.ImageId && i.RecipeId == request.RecipeId, ct);

        if (image is null)
        {
            throw new NotFoundException(nameof(RecipeImage), request.ImageId);
        }

        bool wasPrimary = image.IsPrimary;
        image.IsDeleted = true;
        image.IsPrimary = false;
        images.Update(image);

        if (wasPrimary)
        {
            var nextImage = await images.Query()
                .Where(i => i.RecipeId == request.RecipeId && i.Id != request.ImageId)
                .OrderBy(i => i.OrderIndex)
                .FirstOrDefaultAsync(ct);

            if (nextImage is not null)
            {
                nextImage.IsPrimary = true;
                images.Update(nextImage);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);

        if (cacheInvalidator is not null)
        {
            await cacheInvalidator.EvictByTagAsync($"recipe-{request.RecipeId}", ct);
            await cacheInvalidator.EvictByTagAsync("recipes", ct);
        }
    }
}
