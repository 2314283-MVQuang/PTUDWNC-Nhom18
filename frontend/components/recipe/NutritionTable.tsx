import type { RecipeNutrition } from "@/types/recipe";
import { formatDecimal } from "@/lib/utils";

const ROWS: Array<{ key: keyof RecipeNutrition; label: string; unit: string }> = [
  { key: "calories", label: "Năng lượng", unit: "kcal" },
  { key: "protein", label: "Đạm (Protein)", unit: "g" },
  { key: "carbohydrates", label: "Tinh bột (Carbs)", unit: "g" },
  { key: "fat", label: "Chất béo", unit: "g" },
  { key: "fiber", label: "Chất xơ", unit: "g" },
  { key: "sodium", label: "Natri", unit: "mg" },
];

/** Bảng dinh dưỡng / khẩu phần (RecipeNutrition — Owned Entity, mục 7.2). Ẩn nếu không có dữ liệu nào. */
export function NutritionTable({ nutrition }: { nutrition: RecipeNutrition }) {
  const rows = ROWS.filter((row) => nutrition[row.key] != null);
  if (rows.length === 0) return null;

  return (
    <table className="w-full text-sm">
      <tbody>
        {rows.map((row) => (
          <tr key={row.key} className="border-b border-neutral-100 last:border-0">
            <td className="py-2 text-neutral-600">{row.label}</td>
            <td className="py-2 text-right font-medium text-neutral-900">
              {formatDecimal(nutrition[row.key])} {row.unit}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
