using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandler(
    IRepository<Category> categories,
    IUnitOfWork unitOfWork,
    ISlugGenerator slugGenerator)
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

        var category = new Category
        {
            Name = name,
            Slug = await slugGenerator.GenerateUniqueAsync(name, ct),
            Description = NullIfBlank(request.Description),
            ImageUrl = NullIfBlank(request.ImageUrl),
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