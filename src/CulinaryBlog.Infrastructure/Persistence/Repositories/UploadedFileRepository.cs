using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

public class UploadedFileRepository(CulinaryBlogDbContext db) : IUploadedFileRepository
{
    public async Task<UploadedFile> AddAsync(UploadedFile file, CancellationToken ct = default)
    {
        await db.UploadedFiles.AddAsync(file, ct);
        await db.SaveChangesAsync(ct);
        return file;
    }

    public async Task<UploadedFile?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.UploadedFiles.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<UploadedFile?> GetByStorageKeyAsync(string storageKey, CancellationToken ct = default)
    {
        return await db.UploadedFiles.FirstOrDefaultAsync(x => x.StorageKey == storageKey, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }
}
