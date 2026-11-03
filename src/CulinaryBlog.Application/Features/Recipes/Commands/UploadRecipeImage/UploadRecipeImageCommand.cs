using CulinaryBlog.Application.Common.Authorization;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Features.Recipes.Dtos;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Features.Recipes.Commands.UploadRecipeImage;

public sealed record UploadRecipeImageCommand(Guid RecipeId, string FileName, string ContentType,
    byte[] Content, string? AltText = null) : IRequest<RecipeImageItemDto>;

public sealed class UploadRecipeImageCommandHandler(IRecipeRepository recipes, ICurrentUser user,
    IRecipeImageStorage storage, IRepository<RecipeImage> images, IUnitOfWork unitOfWork,
    ICacheInvalidator cache, ILogger<UploadRecipeImageCommandHandler> logger)
    : IRequestHandler<UploadRecipeImageCommand, RecipeImageItemDto>
{
    public async Task<RecipeImageItemDto> Handle(UploadRecipeImageCommand request, CancellationToken ct)
    {
        var recipe = await recipes.GetByIdAsync(request.RecipeId, ct)
            ?? throw new RecipeNotFoundException(request.RecipeId);
        RecipeAuthorization.EnsureCanModify(recipe, user);
        var extension = RecipeImageFile.Validate(request.FileName, request.ContentType, request.Content);
        if (request.AltText?.Length > 200)
            throw new InvalidFileException("Mô tả ảnh tối đa 200 ký tự.");
        var existing = await images.Query().Where(image => image.RecipeId == recipe.Id).ToListAsync(ct);
        var url = await storage.UploadAsync(recipe.Id, request.Content, request.ContentType.ToLowerInvariant(), extension, ct);
        var image = new RecipeImage { RecipeId = recipe.Id, OriginalUrl = url, AltText = request.AltText?.Trim(),
            IsPrimary = existing.Count == 0, OrderIndex = existing.Count == 0 ? 0 : existing.Max(item => item.OrderIndex) + 1 };
        try
        {
            await images.AddAsync(image, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            // Bù trừ khi lưu metadata thất bại; không để lại đối tượng tải lên không được sử dụng.
            try { await storage.DeleteAsync(recipe.Id, url, CancellationToken.None); }
            catch (Exception ex) { logger.LogError(ex, "Could not clean uploaded object for RecipeId {RecipeId}", recipe.Id); }
            throw;
        }
        // Metadata đã commit: lỗi cache sau đây không được xóa nhầm object đã lưu thành công.
        foreach (var tag in new[] { "recipes", $"recipe-{recipe.Id}", "categories", "search" }) await cache.EvictByTagAsync(tag, ct);
        logger.LogInformation("Recipe image uploaded: RecipeId {RecipeId}, ImageId {ImageId}, UserId {UserId}", recipe.Id, image.Id, user.UserId);
        return new RecipeImageItemDto(image.Id, recipe.Id, url, null, null, image.AltText, image.IsPrimary, image.OrderIndex);
    }
}

public static class RecipeImageFile
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public static string Validate(string name, string contentType, byte[] content)
    {
        if (content.Length is 0 or > MaxBytes) throw new InvalidFileException("Ảnh phải có dung lượng lớn hơn 0 và tối đa 5 MB.");
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var type = contentType.ToLowerInvariant();
        var valid = type switch
        {
            "image/jpeg" => extension is ".jpg" or ".jpeg" && content.Length >= 3 && content[0] == 255 && content[1] == 216 && content[2] == 255,
            "image/png" => extension == ".png" && content.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10}),
            "image/webp" => extension == ".webp" && content.Length >= 12 && content.AsSpan(0,4).SequenceEqual("RIFF"u8) && content.AsSpan(8,4).SequenceEqual("WEBP"u8),
            "image/avif" => extension == ".avif" && content.Length >= 16 && content.AsSpan(4,4).SequenceEqual("ftyp"u8)
                && HasAvifBrand(content),
            _ => false,
        };
        if (!valid) throw new InvalidFileException("Định dạng, MIME type và nội dung ảnh phải khớp JPEG, PNG, WebP hoặc AVIF.");
        return extension == ".jpeg" ? ".jpg" : extension;
    }

    private static bool HasAvifBrand(byte[] content)
    {
        // Major brand hoặc compatible brand, không coi minor-version (offset 12) là brand.
        for (var at = 8; at + 4 <= Math.Min(content.Length, 128); at += 4)
            if (at != 12 && (content.AsSpan(at,4).SequenceEqual("avif"u8) || content.AsSpan(at,4).SequenceEqual("avis"u8))) return true;
        return false;
    }
}
