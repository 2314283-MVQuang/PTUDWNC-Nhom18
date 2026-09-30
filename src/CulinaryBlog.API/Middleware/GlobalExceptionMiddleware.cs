using System.Net;
using System.Text.Json;
using CulinaryBlog.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Middleware;

/// <summary>
/// Mục 5.4: "Global Exception Handler Middleware bắt tất cả unhandled exception → trả 500
/// Problem Details + log". Mọi exception nghiệp vụ ở Application Layer (NotFoundException,
/// ConflictException...) được map sang đúng HTTP status ở đây — Handler không tự set status code.
///
/// CONS-005: response lỗi PHẢI theo RFC 7807 (application/problem+json).
/// TODO (nhóm làm tiếp): bổ sung mã lỗi chi tiết (RECIPE_CONCURRENCY_CONFLICT,
/// CATEGORY_DELETE_HAS_RECIPES...) theo bảng mục 10.2 vào field "type"/"extensions" khi cần.
/// </summary>
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            ValidationException => (422, "Dữ liệu không hợp lệ"),
            InvalidFileException => (400, "Tệp tin không hợp lệ"),
            NotFoundException => ((int)HttpStatusCode.NotFound, "Không tìm thấy tài nguyên"),
            ConflictException => ((int)HttpStatusCode.Conflict, "Xung đột dữ liệu"),
            ForbiddenAccessException => ((int)HttpStatusCode.Forbidden, "Không có quyền truy cập"),
            UnauthorizedException => ((int)HttpStatusCode.Unauthorized, "Không được xác thực"),
            LockedOutException => (423, "Tài khoản tạm khóa"),
            _ => ((int)HttpStatusCode.InternalServerError, "Lỗi hệ thống"),
        };

        if (statusCode == (int)HttpStatusCode.InternalServerError)
        {
            // Không lộ stack trace ra ngoài (mục 5.3) — chỉ log nội bộ.
            logger.LogError(exception, "Lỗi không xác định khi xử lý {Path}", context.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "{StatusCode} khi xử lý {Path}: {Message}", statusCode, context.Request.Path, exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == (int)HttpStatusCode.InternalServerError
                ? "Đã có lỗi xảy ra ở máy chủ. Vui lòng thử lại sau."
                : exception.Message,
            Instance = context.Request.Path,
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}
