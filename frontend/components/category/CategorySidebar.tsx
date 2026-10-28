import Link from "next/link";
import { ErrorState, EmptyState } from "@/components/ui/Feedback";
import { RetryButton } from "@/components/ui/RetryButton";
import type { Category } from "@/types/category";

export function CategorySidebar({ categories, unavailable = false }: { categories: Category[]; unavailable?: boolean }) {
  return <aside aria-label="Danh mục công thức" className="space-y-4 rounded-card-lg border border-brand-100 bg-white p-5 lg:sticky lg:top-28 lg:self-start">
    <h2 className="font-display text-xl font-bold">Danh mục công thức</h2>
    {unavailable ? <ErrorState message="Chưa tải được danh mục. Vui lòng thử lại."><RetryButton /></ErrorState>
      : categories.length === 0 ? <EmptyState title="Chưa có danh mục" message="Các nhóm món ăn sẽ xuất hiện tại đây." />
      : <nav aria-label="Chọn danh mục"><ul className="space-y-2">
        {categories.map(category => <li key={category.id}>
          <Link href={`/categories/${category.slug}`} className="flex items-center justify-between gap-3 rounded-lg px-3 py-2 text-sm hover:bg-brand-50 focus-visible:outline focus-visible:outline-brand-500">
            <span>{category.name}</span><span className="shrink-0 text-neutral-500">{category.recipeCount}</span>
          </Link>
        </li>)}
      </ul></nav>}
    <Link href="/categories" className="inline-block text-sm font-medium text-brand-700 underline">Xem tất cả danh mục</Link>
  </aside>;
}
