namespace CulinaryBlog.Application.Common.Exceptions;

public sealed class CategoryNotFoundException(Guid id)
    : Exception($"Không tìm thấy danh mục với mã '{id}'.");