import type { RecipeNutrition } from "@/types/recipe";
import { NUTRITION_FIELDS } from "@/lib/nutrition";
import { formatDecimal } from "@/lib/utils";

/**
 * Bảng dinh dưỡng / khẩu phần (RecipeNutrition — Owned Entity, mục 7.2.1).
 * Chưa có chỉ số nào thì hiện câu thông báo thay vì để khung trống.
 */
export function NutritionTable({ nutrition, servings }: { nutrition: RecipeNutrition | null | undefined; servings?: number }) {
  const rows = NUTRITION_FIELDS.filter((row) => nutrition?.[row.key] != null);
  const servingsNote = servings ? `Công thức cho ${servings} người · số liệu tính cho 1 khẩu phần.` : "Số liệu tính cho 1 khẩu phần.";

  if (rows.length === 0) {
    return <p className="text-sm text-neutral-500">Tác giả chưa nhập thông tin dinh dưỡng cho công thức này.</p>;
  }

  return (
    <div>
      <p className="mb-2 text-xs text-neutral-500">{servingsNote}</p>
      <table className="w-full text-sm">
        <tbody>
          {rows.map((row) => (
            <tr key={row.key} className="border-b border-neutral-100 last:border-0">
              <td className="py-2 text-neutral-600">{row.label}</td>
              <td className="py-2 text-right font-medium text-neutral-900">
                {formatDecimal(nutrition?.[row.key])} {row.unit}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
