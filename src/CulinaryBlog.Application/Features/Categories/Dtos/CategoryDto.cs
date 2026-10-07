namespace CulinaryBlog.Application.Features.Categories.Dtos;

/// <summary>Khớp đúng type Category bên frontend (frontend/types/category.ts) — có RecipeCount
/// để hiển thị "X công thức" trên mỗi thẻ danh mục.</summary>
public record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? ImageUrl,
    int RecipeCount);