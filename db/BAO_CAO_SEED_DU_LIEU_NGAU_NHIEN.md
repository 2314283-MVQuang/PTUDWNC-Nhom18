# Xác nhận dữ liệu mẫu ngẫu nhiên — Categories & Recipes

Ghi chú xác nhận yêu cầu: *"Đảm bảo đã tạo được cơ sở dữ liệu có chứa dữ liệu ngẫu
nhiên cho ít nhất 20 categories, 100 recipes. Mỗi recipe có ít nhất 10 nguyên liệu và
ít nhất 5 bước chế biến."*

## Cách sinh dữ liệu

Dữ liệu ngẫu nhiên được sinh tự động bằng class
[`Persistence/Seed/DbSeeder.cs`](../src/CulinaryBlog.Infrastructure/Persistence/Seed/DbSeeder.cs)
(dùng thư viện **Bogus**), gọi từ `Program.cs` mỗi khi chạy `dotnet run` ở môi trường
Development. `DbSeeder` tự kiểm tra số lượng hiện có trong database và chỉ chèn thêm
cho tới khi đạt đủ mốc tối thiểu — an toàn khi chạy lại nhiều lần (idempotent), không
sinh trùng lặp.

Schema được tạo bằng EF Core Migration
[`20260923014129_InitialCreate`](../src/CulinaryBlog.Infrastructure/Migrations/20260923014129_InitialCreate.cs).

## Kết quả kiểm tra thực tế (database `CulinaryBlogDb`, 23/09/2026)

Chạy câu SQL sau trực tiếp trên database sau khi `dotnet run` (DbSeeder đã chạy xong):

```sql
WITH counts AS (
  SELECT r."Id", r."Title",
         COUNT(DISTINCT ri."Id") AS ing_count,
         COUNT(DISTINCT rs."Id") AS step_count
  FROM "Recipes" r
  LEFT JOIN "RecipeIngredients" ri ON ri."RecipeId" = r."Id"
  LEFT JOIN "RecipeSteps" rs ON rs."RecipeId" = r."Id"
  GROUP BY r."Id", r."Title"
)
SELECT
  (SELECT COUNT(*) FROM "Categories") AS total_categories,
  (SELECT COUNT(*) FROM counts) AS total_recipes,
  (SELECT MIN(ing_count) FROM counts) AS min_ingredients_per_recipe,
  (SELECT MIN(step_count) FROM counts) AS min_steps_per_recipe,
  (SELECT COUNT(*) FROM counts WHERE ing_count < 10 OR step_count < 5) AS recipes_below_minimum;
```

Kết quả:

| total_categories | total_recipes | min_ingredients_per_recipe | min_steps_per_recipe | recipes_below_minimum |
|---:|---:|---:|---:|---:|
| 20 | 100 | 10 | 5 | 0 |

→ Đạt đủ yêu cầu: ≥20 categories, ≥100 recipes, **mọi** recipe đều có ≥10 nguyên liệu
và ≥5 bước chế biến (không còn recipe nào dưới mức tối thiểu).

> Ghi chú: lần kiểm tra đầu tiên phát hiện 9 recipe cũ (tạo thủ công lúc test app,
> trước khi có `DbSeeder`) không có nguyên liệu/bước nào. Đã xoá 9 recipe đó và chạy
> lại `dotnet run` để `DbSeeder` sinh bù đủ 100 recipe, tất cả đều đạt chuẩn.
