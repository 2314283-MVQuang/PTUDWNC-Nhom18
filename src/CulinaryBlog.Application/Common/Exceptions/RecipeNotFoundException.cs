namespace CulinaryBlog.Application.Common.Exceptions;

/// <summary>
/// Lỗi nghiệp vụ riêng của Recipe khi không tìm thấy công thức.
/// Middleware toàn cục sẽ chuyển lỗi này thành HTTP 404
/// với mã lỗi RECIPE_NOT_FOUND.
/// </summary>
public sealed class RecipeNotFoundException(object key)
    : Exception($"Không tìm thấy công thức với khóa '{key}'.");