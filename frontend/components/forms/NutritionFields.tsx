import type { FieldErrors, UseFormRegister } from "react-hook-form";
import { Input } from "@/components/ui/Input";
import { NUTRITION_FIELDS, NUTRITION_MAX } from "@/lib/nutrition";
import type { RecipeBasicInfoValues } from "@/lib/validations/recipe.schema";

/**
 * 6 ô nhập dinh dưỡng cho 1 khẩu phần (RecipeNutrition, SRS mục 7.2.1) — dùng chung cho
 * bước 1 của form tạo công thức và form chỉnh sửa. Không bắt buộc: để trống ô nào thì
 * trang chi tiết bỏ qua dòng đó.
 */
export function NutritionFields({
  register,
  errors,
}: {
  register: UseFormRegister<RecipeBasicInfoValues>;
  errors?: FieldErrors<RecipeBasicInfoValues>["nutrition"];
}) {
  return (
    <fieldset className="rounded-card border border-neutral-200 p-4">
      <legend className="px-1 text-sm font-medium text-neutral-800">Dinh dưỡng cho 1 khẩu phần</legend>
      <p className="mb-3 text-xs text-neutral-500">Không bắt buộc — bỏ trống những chỉ số bạn chưa biết.</p>
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
        {NUTRITION_FIELDS.map(({ key, label, unit }) => (
          <Input
            key={key}
            label={`${label} (${unit})`}
            type="number"
            inputMode="decimal"
            min={0}
            max={NUTRITION_MAX}
            step="any"
            error={errors?.[key]?.message}
            {...register(`nutrition.${key}`)}
          />
        ))}
      </div>
    </fieldset>
  );
}
