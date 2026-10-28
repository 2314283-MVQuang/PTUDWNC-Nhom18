import { BookOpen, Grid3X3, LayoutList } from "lucide-react";
import { connection } from "next/server";
import { apiFetch } from "@/lib/api-client";
import { CategoryGrid } from "@/components/category/CategoryGrid";
import { PageHero } from "@/components/layout/PageHero";
import type { Category } from "@/types/category";
import { ErrorState } from "@/components/ui/Feedback";
import { RetryButton } from "@/components/ui/RetryButton";

// HTML theo request; dữ liệu danh mục thành công vẫn cache 1 giờ.
export const revalidate = 3600;

export default async function CategoriesPage() {
  await connection();
  // Bọc try-catch để backend chưa chạy thì trang vẫn render được (xem chú thích ở trang chủ).
  let categories: Category[] = [];
  let backendError = false;

  try {
    categories = await apiFetch<Category[]>("/categories", { next: { revalidate } });
  } catch {
    backendError = true;
  }

  // Tổng công thức
  const totalRecipes = categories.reduce((s, c) => s + c.recipeCount, 0);

  // Sắp xếp: danh mục có nhiều công thức nhất lên trước
  const sorted = [...categories].sort((a, b) => b.recipeCount - a.recipeCount);

  // Danh mục nổi bật (top 3 có nhiều công thức nhất)
  const featured = sorted.slice(0, 3);

  return (
    <div className="flex flex-col">
      <PageHero
        title="Danh mục công thức"
        description="Từ món khai vị đến tráng miệng — chọn nhóm món để xem toàn bộ công thức thuộc nhóm đó."
        emojis={["🥗", "🍰", "🍲"]}
      >
        <div className="flex flex-wrap gap-3">
          <span className="inline-flex items-center gap-2 rounded-full border border-white/30 bg-white/15 px-4 py-1.5 text-sm font-medium text-white backdrop-blur-md">
            <Grid3X3 className="h-4 w-4" />
            {categories.length} danh mục
          </span>
          <span className="inline-flex items-center gap-2 rounded-full border border-white/30 bg-white/15 px-4 py-1.5 text-sm font-medium text-white backdrop-blur-md">
            <BookOpen className="h-4 w-4" />
            {totalRecipes} công thức
          </span>
        </div>
      </PageHero>

      <div className="container-page flex flex-col gap-10 pb-20 pt-10">
        {backendError && (
          <ErrorState message="Không tải được danh mục. Vui lòng thử lại."><RetryButton /></ErrorState>
        )}

        {/* Danh mục nổi bật (top 3) */}
        {featured.length > 0 && (
          <section>
            <div className="mb-4 flex items-center gap-2">
              <span className="text-lg">🔥</span>
              <h2 className="text-lg font-bold text-neutral-900">Danh mục nổi bật</h2>
              <span className="ml-1 text-sm text-neutral-400">Nhiều công thức nhất</span>
            </div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              {featured.map((cat) => (
                <a
                  key={cat.id}
                  href={`/categories/${cat.slug}`}
                  className="group flex items-center gap-4 rounded-card-lg border border-brand-100 bg-white p-4 shadow-soft transition-all duration-300 hover:-translate-y-0.5 hover:border-brand-300 hover:shadow-lift"
                >
                  <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-xl bg-brand-gradient text-3xl shadow-sm">
                    {getEmoji(cat.name)}
                  </span>
                  <div className="min-w-0">
                    <p className="truncate font-bold text-neutral-900 group-hover:text-brand-700">{cat.name}</p>
                    {cat.description && (
                      <p className="mt-0.5 line-clamp-1 text-xs text-neutral-500">{cat.description}</p>
                    )}
                    <p className="mt-1 text-xs font-semibold text-brand-600">{cat.recipeCount} công thức</p>
                  </div>
                </a>
              ))}
            </div>
          </section>
        )}

        {/* Toàn bộ danh mục dạng lưới */}
        {!backendError && <section>
          <div className="mb-4 flex items-center gap-2">
            <LayoutList className="h-5 w-5 text-neutral-400" />
            <h2 className="text-lg font-bold text-neutral-900">Tất cả danh mục</h2>
            <span className="ml-1 text-sm text-neutral-400">({categories.length})</span>
          </div>
          <CategoryGrid categories={sorted} />
        </section>}
      </div>
    </div>
  );
}

/** Hàm helper dùng inline trong trang (không muốn import lại từ CategoryCard) */
function getEmoji(name: string): string {
  const MAP: Array<[RegExp, string]> = [
    [/khai vị|salad|gỏi/i, "🥗"],
    [/tráng miệng|ngọt|chè/i, "🍮"],
    [/bánh/i, "🍰"],
    [/súp|canh/i, "🍵"],
    [/lẩu|nồi/i, "🍲"],
    [/mì|phở|bún|noodle|miến/i, "🍜"],
    [/nướng|bbq|rang/i, "🍖"],
    [/hải sản|cá|tôm|mực/i, "🦐"],
    [/chay/i, "🥬"],
    [/đồ uống|nước|sinh tố/i, "🥤"],
    [/sáng|breakfast/i, "🍳"],
    [/chiên|xào/i, "🥘"],
    [/hấp/i, "♨️"],
    [/kho/i, "🍛"],
    [/Nhật|Hàn|Tàu|Ý|Tây/i, "🌍"],
    [/eat clean|healthy/i, "🥑"],
  ];
  for (const [p, e] of MAP) if (p.test(name)) return e;
  return "🍽️";
}
