namespace CulinaryBlog.Application.Common.Exceptions;

/// <summary>
/// RowVersion mà client gửi trong If-Match đã cũ.
/// GlobalExceptionMiddleware sẽ chuyển lỗi này thành HTTP 409
/// với mã RECIPE_CONCURRENCY_CONFLICT.
/// </summary>
public sealed class RecipeConcurrencyConflictException()
    : Exception("Dữ liệu Recipe đã bị thay đổi bởi người dùng khác. Vui lòng tải lại trang.");
