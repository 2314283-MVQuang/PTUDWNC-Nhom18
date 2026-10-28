import type { RecipeIngredient } from "@/types/recipe";
import { formatDecimal } from "@/lib/utils";

/** Danh sách nguyên liệu, hiển thị ở trang chi tiết recipe (mục 7.4). */
export function IngredientList({ ingredients }: { ingredients: RecipeIngredient[] }) {
  if (ingredients.length === 0) {
    return <p className="text-sm text-neutral-500">Chưa có nguyên liệu.</p>;
  }

  return (
    <ul className="divide-y divide-neutral-100">
      {ingredients
        .slice()
        .sort((a, b) => a.orderIndex - b.orderIndex)
        .map((ing) => (
          <li key={ing.id} className="flex items-baseline justify-between gap-4 py-2 text-sm">
            <span className="text-neutral-800">{ing.name}</span>
            <span className="whitespace-nowrap text-neutral-500">
              {formatDecimal(ing.quantity)} {ing.unit ?? ""}
            </span>
          </li>
        ))}
    </ul>
  );
}
