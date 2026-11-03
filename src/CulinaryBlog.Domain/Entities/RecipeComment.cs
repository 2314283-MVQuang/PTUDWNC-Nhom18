using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// Tuần 9 (Quang) — FR-CMT (bổ sung SRS v1.1.0): bình luận của người dùng dưới 1 công thức
/// (bảng "RecipeComments"). Xoá mềm như mọi entity khác (IsDeleted/DeletedAt).
/// </summary>
public class RecipeComment : BaseEntity
{
    public Guid RecipeId { get; set; }

    public Recipe Recipe { get; set; } = null!;

    public string AuthorId { get; set; } = null!;

    public ApplicationUser Author { get; set; } = null!;

    /// <summary>Nội dung thuần văn bản, 1–1000 ký tự sau khi trim. Frontend hiển thị như text, không render HTML.</summary>
    public string Content { get; set; } = null!;

    /// <summary>true khi tác giả bình luận đã sửa nội dung ít nhất 1 lần (hiện nhãn "đã chỉnh sửa").</summary>
    public bool IsEdited { get; set; }

    public const int MaxContentLength = 1000;
}
