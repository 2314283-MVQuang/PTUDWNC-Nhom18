using Serilog.Context;

namespace CulinaryBlog.API.Middleware;

/// <summary>
/// FR-OBS-002 (Tuần 3) — gắn 1 CorrelationId cho mỗi request: đọc từ header "X-Correlation-Id"
/// nếu client (frontend, hoặc service khác) đã gửi sẵn — để nối trace xuyên nhiều lời gọi/nhiều
/// service — sinh mới (Guid) nếu không có. Trả lại đúng giá trị đó trong response header để
/// client biết ID nào để đối chiếu log khi báo lỗi.
///
/// Đẩy CorrelationId vào Serilog LogContext (LogContext.PushProperty) để MỌI dòng log phát sinh
/// trong lúc xử lý request này — kể cả log ở LoggingBehavior (Application layer, mục 6.3), sâu
/// trong MediatR pipeline — tự động có field "CorrelationId" mà không cần truyền tay qua từng
/// tầng (Application layer không biết gì về HTTP, không thể tự đọc header).
///
/// Đăng ký TRƯỚC GlobalExceptionMiddleware trong Program.cs — kể cả request bị lỗi 500 cũng cần
/// có CorrelationId trong log để tra được.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
            && !string.IsNullOrWhiteSpace(existing)
                ? existing.ToString()
                : Guid.NewGuid().ToString();

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
