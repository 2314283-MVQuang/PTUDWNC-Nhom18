using CulinaryBlog.Application.Common.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CulinaryBlog.API.Extensions;

/// <summary>
/// Mục 8: "Response format thành công: { "data": {...}, "meta": { "page", "pageSize", "total",
/// "totalPages" } }". Dùng 2 extension này ở MỌI endpoint để không lặp lại cấu trúc JSON thủ công.
/// </summary>
public static class ApiResponseExtensions
{
    public static Ok<ApiEnvelope<T>> ToOkResponse<T>(this T data) => TypedResults.Ok(new ApiEnvelope<T>(data));

    public static Created<ApiEnvelope<T>> ToCreatedResponse<T>(
        this T data,
        string location) =>
            TypedResults.Created(location, new ApiEnvelope<T>(data));

    public static Ok<PagedApiEnvelope<T>> ToPagedResponse<T>(this PagedResult<T> paged) =>
        TypedResults.Ok(new PagedApiEnvelope<T>(paged.Items, new PageMetadata(
            paged.Page, paged.PageSize, paged.TotalCount, paged.TotalPages,
            paged.HasNextPage, paged.HasPreviousPage)));
}

public sealed record ApiEnvelope<T>(T Data);
public sealed record PagedApiEnvelope<T>(IReadOnlyList<T> Data, PageMetadata Meta);
public sealed record PageMetadata(int Page, int PageSize, int Total, int TotalPages,
    bool HasNextPage, bool HasPreviousPage);
