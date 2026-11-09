using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Features.Files.Commands.DeleteFile;

public sealed class DeleteFileCommandHandler(
    IFileStorageService fileStorageService,
    IUploadedFileRepository? uploadedFiles = null)
    : IRequestHandler<DeleteFileCommand>
{
    public async Task Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ObjectName))
        {
            throw new ArgumentException("Tên object cần xóa không được để trống.", nameof(request.ObjectName));
        }

        await fileStorageService.DeleteAsync(request.ObjectName, cancellationToken);

        // Tuần 10 (Quang): file đã xoá khỏi MinIO thì đánh dấu bản ghi metadata là đã xoá, để
        // "Thư viện ảnh của tôi" (GET /files/mine) không hiện ảnh hỏng. Object medium/thumbnail
        // không có bản ghi riêng nên bỏ qua.
        if (uploadedFiles is not null)
        {
            var record = await uploadedFiles.GetByStorageKeyAsync(request.ObjectName, cancellationToken);
            if (record is not null && !record.IsDeleted)
            {
                record.IsDeleted = true;
                await uploadedFiles.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
