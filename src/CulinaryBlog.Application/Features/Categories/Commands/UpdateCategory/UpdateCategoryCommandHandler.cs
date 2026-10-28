using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;

public class UpdateCategoryCommandHandler(
    IRepository<Category> categories,
    IUnitOfWork unitOfWork,
    ICacheInvalidator? cacheInvalidator = null)
    : IRequestHandler<UpdateCategoryCommand, Category>
{
    public async Task<Category> Handle(UpdateCategoryCommand request, CancellationToken ct)
    {
        var category = await categories.Query()
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        if (category is null)
        {
            throw new NotFoundException(nameof(Category), request.Id);
        }

        var trimmedName = request.Name.Trim();
        var isNameChanged = !string.Equals(category.Name, trimmedName, StringComparison.Ordinal);

        if (isNameChanged)
        {
            // Name là duy nhất (SRS FR-CAT-003 + unique index "IX_Categories_Name") — kiểm tra trước để
            // trả 409 rõ ràng thay vì để PostgreSQL ném lỗi unique constraint (thành 500).
            var normalizedName = trimmedName.ToUpperInvariant();
            var nameExists = await categories.Query()
                .IgnoreQueryFilters()
                .AnyAsync(c => c.Id != category.Id && c.Name.ToUpper() == normalizedName, ct);

            if (nameExists)
            {
                throw new ConflictException($"Danh mục '{trimmedName}' đã tồn tại.");
            }

            category.Name = trimmedName;

            // SRS FR-CAT-004: "Slug KHÔNG thay đổi khi đổi tên" — giữ slug cũ để không làm hỏng
            // URL /categories/{slug} đã chia sẻ, sitemap và SEO. (Tuần 8 - Thịnh: bỏ sinh lại slug.)
        }

        category.Description = NullIfBlank(request.Description);
        category.ImageUrl = NullIfBlank(request.ImageUrl);

        await unitOfWork.SaveChangesAsync(ct);

        // Xóa cache categories ngay sau khi cập nhật
        if (cacheInvalidator is not null)
        {
            await cacheInvalidator.EvictByTagAsync("categories", ct);
        }

        return category;
    }

    private static string? NullIfBlank(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
