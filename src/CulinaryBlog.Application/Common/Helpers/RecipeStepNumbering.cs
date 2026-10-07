namespace CulinaryBlog.Application.Common.Helpers;

/// <summary>
/// Quy ước đánh số tạm cho RecipeStep khi sắp xếp lại / xoá mềm.
///
/// Bảng "RecipeSteps" trong PostgreSQL có 2 ràng buộc (xem db/init/02-schema.sql):
///   - CHECK ("StepNumber" > 0)                       → KHÔNG được dùng số âm làm số tạm.
///   - UNIQUE ("RecipeId", "StepNumber")              → tính cả các dòng đã xoá mềm.
/// Vì vậy mọi số tạm đều là số DƯƠNG, nằm ở 2 dải tách biệt với số bước thật (1, 2, 3...):
///   - <see cref="TempBase"/> + i     : số tạm trong lúc đổi thứ tự (bước 1 của phép đổi 2 pha).
///   - lớn hơn <see cref="DeletedBase"/> : chỗ "đỗ" vĩnh viễn của bước đã xoá mềm.
/// </summary>
public static class RecipeStepNumbering
{
    /// <summary>Gốc của dải số tạm khi đổi thứ tự. Một công thức không bao giờ có tới 500.000 bước.</summary>
    public const int TempBase = 500_000;

    /// <summary>Gốc của dải số dành cho bước đã xoá mềm.</summary>
    public const int DeletedBase = 1_000_000;
}
