using CulinaryBlog.Application.Features.Files.Commands.GeneratePresignedUrl;
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
        var group = app.MapGroup("/api/v1/files").WithTags("Files");

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
    }
}
