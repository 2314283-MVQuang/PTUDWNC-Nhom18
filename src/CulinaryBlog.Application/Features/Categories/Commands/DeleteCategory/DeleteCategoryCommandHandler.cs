using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Features.Categories.Commands.DeleteCategory;

public class DeleteCategoryCommandHandler(
    ICategoryRepository categories,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteCategoryCommand>
{
    public async Task Handle(DeleteCategoryCommand request, CancellationToken ct)
    {
        var category = await categories.GetByIdAsync(request.Id, ct)
            ?? throw new CategoryNotFoundException(request.Id);

        if (await categories.HasRecipesAsync(category.Id, ct))
        {
            throw new ConflictException("Không thể xóa danh mục đang có công thức.");
        }

        category.IsDeleted = true;
        categories.Update(category);
        await unitOfWork.SaveChangesAsync(ct);
    }
}