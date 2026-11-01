using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Files.Commands.GeneratePresignedUrl;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Features.Files.Queries.GetUploadedFileById;
using MediatR;

namespace CulinaryBlog.API.Endpoints;

public record GeneratePresignedUrlRequest(
    string FileName,
    string ContentType,
    long Size
);

public static class FileEndpoints
{
    public static void MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /uploads/{*filePath}: Phục vụ ảnh cục bộ công khai (fallback khi MinIO offline)
        app.MapGet("/uploads/{*filePath}", (string filePath) =>
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "uploads");
            var clean = filePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(folder, clean);
            if (!File.Exists(fullPath)) return Results.NotFound();

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var mime = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
            return Results.File(fullPath, mime);
        }).AllowAnonymous();

        var group = app.MapV1Group("/files").WithTags("Files")
            .RequireAuthorization(AuthorizationPolicies.Author);

        // FR-FILE-001: Sinh Presigned PUT URL để upload ảnh lên MinIO và lưu metadata vào CSDL
        group.MapPost("/presigned-url", async (GeneratePresignedUrlRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new GeneratePresignedUrlCommand(
                request.FileName,
                request.ContentType,
                request.Size
            );

            var result = await sender.Send(command, ct);
            return Results.Ok(new { data = result });
        })
        .WithName("GeneratePresignedUrl")
        .WithSummary("Sinh presigned upload URL cho MinIO và lưu metadata tệp")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // GET /api/v1/files/{id}: Lấy metadata của tệp tin theo ID
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUploadedFileByIdQuery(id), ct);
            return Results.Ok(new { data = result });
        })
        .WithName("GetUploadedFileById")
        .WithSummary("Lấy thông tin metadata của tệp tin theo ID")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /api/v1/files/upload-direct: Fallback upload trực tiếp qua backend khi MinIO offline.
        // Frontend gọi endpoint này bằng multipart/form-data khi presigned URL không khả dụng hoặc MinIO không phản hồi.
        // File được lưu vào AppContext.BaseDirectory/uploads và phục vụ tĩnh qua GET /uploads/...
        group.MapPost("/upload-direct", async (HttpRequest request, IFileStorageService fileStorage, CancellationToken ct) =>
        {
            if (!request.HasFormContentType || request.Form.Files.Count == 0)
                return Results.BadRequest(new { title = "File rỗng hoặc không được chọn." });

            var file = request.Form.Files[0];
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { title = "File rỗng hoặc không được chọn." });

            var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "image/jpeg", "image/png", "image/webp", "image/gif" };
            var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExts.Contains(ext) || !allowedTypes.Contains(file.ContentType))
                return Results.BadRequest(new { title = "Chỉ nhận ảnh JPEG, PNG, WebP hoặc GIF." });

            if (file.Length > 10 * 1024 * 1024)
                return Results.BadRequest(new { title = "Ảnh vượt quá dung lượng tối đa 10MB." });

            var storageKey = $"uploads/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext}";
            await using var stream = file.OpenReadStream();
            var url = await fileStorage.UploadFileAsync(storageKey, stream, file.ContentType, ct);

            return Results.Ok(new { data = new { url, storageKey } });
        })
        .AllowAnonymous()
        .DisableAntiforgery()
        .WithName("UploadFileDirect")
        .WithSummary("Upload ảnh trực tiếp qua backend (fallback khi MinIO offline)")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
