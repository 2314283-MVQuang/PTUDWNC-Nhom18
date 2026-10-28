using MediatR;

namespace CulinaryBlog.Application.Features.Files.Commands.DeleteFile;

public record DeleteFileCommand(string ObjectName) : IRequest;
