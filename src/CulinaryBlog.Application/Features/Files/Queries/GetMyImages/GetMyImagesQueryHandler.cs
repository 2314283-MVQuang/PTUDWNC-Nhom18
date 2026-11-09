using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Files.Queries.GetMyImages;

public sealed class GetMyImagesQueryHandler(IRepository<UploadedFile> files, ICurrentUser currentUser)
    : IRequestHandler<GetMyImagesQuery, PagedResult<MyImageDto>>
{
    /// <summary>Khớp AllowedContentTypes khi cấp presigned URL (FR-FILE-001).</summary>
    private static readonly string[] ImageContentTypes = ["image/jpeg", "image/png", "image/webp", "image/avif"];

    public async Task<PagedResult<MyImageDto>> Handle(GetMyImagesQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedException("Bạn cần đăng nhập để xem thư viện ảnh.");
        }

        // Bỏ file đã xoá khỏi MinIO (DeleteFileCommandHandler đánh dấu IsDeleted; UploadedFile không
        // có Global Query Filter nên lọc tay). Chỉ lấy ảnh của CHÍNH người dùng, kể cả Admin.
        var query = files.Query()
            .AsNoTracking()
            .Where(f => !f.IsDeleted && f.UploadedBy == userId && ImageContentTypes.Contains(f.ContentType));

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .ThenBy(f => f.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(f => new MyImageDto(f.Id, f.Url, f.ThumbnailUrl, f.FileName, f.ContentType, f.Size, f.CreatedAt))
            .ToListAsync(ct);

        return PagedResult<MyImageDto>.Create(items, request.Page, request.PageSize, totalCount);
    }
}
