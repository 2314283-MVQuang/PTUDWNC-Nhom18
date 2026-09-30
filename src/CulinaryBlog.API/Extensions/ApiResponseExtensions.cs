using CulinaryBlog.Application.Common.Models;

namespace CulinaryBlog.API.Extensions;

/// <summary>
/// Mục 8: "Response format thành công: { "data": {...}, "meta": { "page", "pageSize", "total",
/// "totalPages" } }". Dùng 2 extension này ở MỌI endpoint để không lặp lại cấu trúc JSON thủ công.
/// </summary>
public static class ApiResponseExtensions
{
    public static IResult ToOkResponse<T>(this T data) => Results.Ok(new { data });

    public static IResult ToCreatedResponse<T>(
        this T data,
        string location) =>
        Results.Created(
            location,
            new
            {
                data
            });

    public static IResult ToPagedResponse<T>(this PagedResult<T> paged) =>
        Results.Ok(new
        {
            data = paged.Items,
            meta = new
            {
                page = paged.Page,
                pageSize = paged.PageSize,
                total = paged.TotalCount,
                totalPages = paged.TotalPages,
                hasNextPage = paged.HasNextPage,
                hasPreviousPage = paged.HasPreviousPage,
            },
        });
}
