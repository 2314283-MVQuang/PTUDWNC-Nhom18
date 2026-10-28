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

  return (
    <div className="flex flex-col">
      <PageHero
        title="Danh mục công thức"
        description="Từ món khai vị đến tráng miệng — chọn nhóm món để xem toàn bộ công thức thuộc nhóm đó."
        emojis={["🥗", "🍰", "🍲"]}
      >
        <span className="inline-flex items-center gap-2 rounded-full border border-white/30 bg-white/15 px-4 py-1.5 text-sm font-medium text-white backdrop-blur-md">
          {backendError ? "Chưa tải được danh mục" : `${categories.length} danh mục`}
        </span>
      </PageHero>

      <div className="container-page flex flex-col gap-6 pb-20 pt-8">
        {backendError ? <ErrorState message="Không tải được danh mục. Vui lòng thử lại."><RetryButton /></ErrorState>
          : <CategoryGrid categories={categories} />}
      </div>
    </div>
  );
}
