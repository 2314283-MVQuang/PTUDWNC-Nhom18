import { z } from "zod";

/** Khớp ràng buộc bảng "Recipes" (mục 7.2): PrepTime>0, CookTime>=0, Servings>0. */
export const recipeBasicInfoSchema = z.object({
  title: z.string().trim().min(5, "Tên công thức phải có ít nhất 5 ký tự").max(200, "Tối đa 200 ký tự"),
  description: z.string().min(1, "Vui lòng nhập mô tả").max(2000, "Tối đa 2000 ký tự"),
  categoryId: z.string().min(1, "Vui lòng chọn danh mục"),
  prepTimeMinutes: z.coerce.number().int().positive("Thời gian chuẩn bị phải lớn hơn 0"),
  cookTimeMinutes: z.coerce.number().int().min(0, "Thời gian nấu không được âm"),
  servings: z.coerce.number().int().positive("Số khẩu phần phải lớn hơn 0"),
  difficulty: z.enum(["Easy", "Medium", "Hard", "Expert"]),
});
export type RecipeBasicInfoValues = z.infer<typeof recipeBasicInfoSchema>;

/** Khớp bảng "RecipeIngredients" (mục 7.4) — dùng cho form thêm nguyên liệu trong wizard. */
export const recipeIngredientSchema = z.object({
  name: z.string().min(1, "Vui lòng nhập tên nguyên liệu").max(200),
  quantity: z.coerce.number().positive("Số lượng phải lớn hơn 0").optional(),
  unit: z.string().max(50).optional(),
  notes: z.string().max(500).optional(),
});
export type RecipeIngredientValues = z.infer<typeof recipeIngredientSchema>;

/** Khớp bảng "RecipeSteps" (mục 7.3) — dùng cho form thêm bước thực hiện trong wizard. */
export const recipeStepSchema = z.object({
  title: z.string().min(1, "Vui lòng nhập tiêu đề bước").max(200),
  description: z.string().min(1, "Vui lòng mô tả bước thực hiện").max(2000, "Tối đa 2000 ký tự"),
  timerMinutes: z.coerce.number().int().min(0).optional(),
});
export type RecipeStepValues = z.infer<typeof recipeStepSchema>;

/** Khớp body POST /categories (FR-CAT-003). */
export const categorySchema = z.object({
  name: z.string().min(1, "Vui lòng nhập tên danh mục").max(100),
  description: z.string().max(1000).optional(),
});
export type CategoryFormValues = z.infer<typeof categorySchema>;
