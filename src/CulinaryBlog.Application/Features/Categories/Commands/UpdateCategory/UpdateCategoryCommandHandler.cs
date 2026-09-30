using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;

public class UpdateCategoryCommandHandler(
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    ISlugGenerator slugGenerator)
    : IRequestHandler<UpdateCategoryCommand, Category>
{
    public async Task<Category> Handle(UpdateCategoryCommand request, CancellationToken ct)
    {
        var category = await categories.GetByIdAsync(request.Id, ct)
            ?? throw new CategoryNotFoundException(request.Id);

        var name = request.Name.Trim();
        var normalizedName = name.ToUpperInvariant();
        var nameExists = await categories.Query()
            .IgnoreQueryFilters()
            .AnyAsync(existing => existing.Id != category.Id
                && existing.Name.ToUpper() == normalizedName, ct);

        if (nameExists)
        {
            throw new ConflictException($"Danh mục '{name}' đã tồn tại.");
        }

        if (!string.Equals(category.Name, name, StringComparison.Ordinal))
        {
            category.Slug = await slugGenerator.GenerateUniqueAsync(name, ct, category.Id);
        }

        category.Name = name;
        category.Description = NullIfBlank(request.Description);
        category.ImageUrl = NullIfBlank(request.ImageUrl);

        categories.Update(category);
        await unitOfWork.SaveChangesAsync(ct);

        return category;
    }

    private static string? NullIfBlank(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}