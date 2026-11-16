import { z } from "zod";
import type { RecipeNutrition } from "@/types/recipe";

/**
 * Dinh dưỡng cho 1 khẩu phần — RecipeNutrition (SRS mục 7.2.1), trường tuỳ chọn của
 * FR-RCP-003 (tạo) và FR-RCP-004 (cập nhật). Dùng chung cho form và bảng hiển thị.
 */
export const NUTRITION_FIELDS: ReadonlyArray<{ key: keyof RecipeNutrition; label: string; unit: string }> = [
  { key: "calories", label: "Năng lượng", unit: "kcal" },
  { key: "protein", label: "Đạm (Protein)", unit: "g" },
  { key: "carbohydrates", label: "Tinh bột (Carbs)", unit: "g" },
  { key: "fat", label: "Chất béo", unit: "g" },
  { key: "fiber", label: "Chất xơ", unit: "g" },
  { key: "sodium", label: "Natri", unit: "mg" },
];

/** Cột decimal(8,2): giới hạn 99 999 để không tràn số và chặn giá trị vô lý. */
export const NUTRITION_MAX = 99999;

/** Ô bỏ trống ("") → undefined; còn lại phải là số trong [0, NUTRITION_MAX]. */
const optionalNutrient = z.preprocess(
  (value) => (value === "" || value === null || value === undefined ? undefined : Number(value)),
  z
    .number({ invalid_type_error: "Vui lòng nhập số" })
    .min(0, "Không được âm")
    .max(NUTRITION_MAX, `Tối đa ${NUTRITION_MAX}`)
    .optional(),
);

export const recipeNutritionSchema = z.object({
  calories: optionalNutrient,
  protein: optionalNutrient,
  carbohydrates: optionalNutrient,
  fat: optionalNutrient,
  fiber: optionalNutrient,
  sodium: optionalNutrient,
});
export type RecipeNutritionValues = z.infer<typeof recipeNutritionSchema>;

/** Có ít nhất một chỉ số dinh dưỡng được nhập. */
export function hasNutrition(nutrition: Partial<Record<keyof RecipeNutrition, unknown>> | null | undefined): boolean {
  return !!nutrition && NUTRITION_FIELDS.some(({ key }) => nutrition[key] !== null && nutrition[key] !== undefined);
}

/**
 * Giá trị form → body API (bỏ trống thì gửi null).
 * - Tạo mới: không nhập gì thì trả undefined để không gửi key `nutrition`.
 * - Cập nhật (`keepEmpty`): luôn gửi đủ 6 trường, nhờ vậy xoá trắng một ô sẽ xoá giá trị cũ
 *   (backend thay nguyên khối Nutrition khi body có `nutrition`).
 */
export function toNutritionInput(values: RecipeNutritionValues | undefined, keepEmpty = false): RecipeNutrition | undefined {
  if (!keepEmpty && !hasNutrition(values)) return undefined;
  const result = {} as RecipeNutrition;
  for (const { key } of NUTRITION_FIELDS) result[key] = values?.[key] ?? null;
  return result;
}

/** Dữ liệu từ API (null) → giá trị mặc định của form (undefined = ô trống). */
export function nutritionFormDefaults(nutrition: RecipeNutrition | null | undefined): RecipeNutritionValues {
  const result: RecipeNutritionValues = {};
  for (const { key } of NUTRITION_FIELDS) {
    const value = nutrition?.[key];
    if (value !== null && value !== undefined) result[key] = Number(value);
  }
  return result;
}
