using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Features.Files.Commands.DeleteFile;

public sealed class DeleteFileCommandHandler(IFileStorageService fileStorageService)
    : IRequestHandler<DeleteFileCommand>
{
    public Task Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ObjectName))
        {
            throw new ArgumentException("Tên object cần xóa không được để trống.", nameof(request.ObjectName));
        }

        return fileStorageService.DeleteAsync(request.ObjectName, cancellationToken);
    }
}
