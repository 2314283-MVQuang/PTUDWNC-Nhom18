namespace CulinaryBlog.Application.Common.Exceptions;

/// <summary>
/// Không tìm thấy danh mục (mã lỗi CATEGORY_NOT_FOUND, HTTP 404 — Phụ lục B của SRS).
/// Tra theo ID (sửa/xoá danh mục) hoặc theo slug (FR-CAT-002, trang chi tiết danh mục).
/// </summary>
public sealed class CategoryNotFoundException : Exception
{
    public CategoryNotFoundException(Guid id)
        : base($"Không tìm thấy danh mục với mã '{id}'.")
    {
    }

    public CategoryNotFoundException(string slug)
        : base($"Không tìm thấy danh mục với slug '{slug}'.")
    {
    }
}
