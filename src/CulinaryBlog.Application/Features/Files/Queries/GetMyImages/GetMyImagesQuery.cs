using CulinaryBlog.Application.Common.Models;
using MediatR;

namespace CulinaryBlog.Application.Features.Files.Queries.GetMyImages;

/// <summary>
/// Tuần 10 (Quang) — "Thư viện ảnh của tôi": GET /api/v1/files/mine. Liệt kê ảnh người dùng hiện
/// tại đã tải lên hệ thống (mới nhất trước) để chọn lại cho bước thực hiện / danh mục mà không phải
/// tìm file trên máy. Frontend dùng lại một ảnh bằng cách tải lên BẢN SAO mới (mỗi file chỉ gắn một
/// chỗ — xem RecipeImageUploadGuard).
/// </summary>
public sealed record GetMyImagesQuery(int Page = 1, int PageSize = 24) : IRequest<PagedResult<MyImageDto>>
{
    public const int MaxPageSize = 48;
}

public sealed record MyImageDto(
    Guid Id,
    string Url,
    string? ThumbnailUrl,
    string FileName,
    string ContentType,
    long Size,
    DateTimeOffset CreatedAt);
