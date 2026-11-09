using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandler(
    IRepository<Category> categories,
    IUnitOfWork unitOfWork,
    ISlugGenerator slugGenerator,
    RecipeImageUploadGuard imageGuard)
    : IRequestHandler<CreateCategoryCommand, Category>
{
    public async Task<Category> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        var normalizedName = name.ToUpperInvariant();
        var nameExists = await categories.Query()
            .IgnoreQueryFilters()
            .AnyAsync(category => category.Name.ToUpper() == normalizedName, ct);

        if (nameExists)
        {
            throw new ConflictException($"Danh mục '{name}' đã tồn tại.");
        }

        // Tuần 10 (Quang): ảnh danh mục phải tải lên qua hệ thống (presigned URL), đúng là ảnh,
        // ≤ 5MB và chưa dùng ở chỗ khác — xem RecipeImageUploadGuard.
        var imageUrl = NullIfBlank(request.ImageUrl);
        if (imageUrl is not null)
        {
            await imageGuard.EnsureCanAttachAsync(imageUrl, exceptStepId: null, ct);
        }

        var category = new Category
        {
            Name = name,
            Slug = await slugGenerator.GenerateUniqueAsync(name, ct),
            Description = NullIfBlank(request.Description),
            ImageUrl = imageUrl,
        };

        await categories.AddAsync(category, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return category;
    }

    private static string? NullIfBlank(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}