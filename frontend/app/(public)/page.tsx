import { connection } from "next/server";
import { apiFetch, apiFetchPaged } from "@/lib/api-client";
import { RecipeGrid } from "@/components/recipe/RecipeGrid";
import { CategoryGrid } from "@/components/category/CategoryGrid";
import { HeroSection } from "@/components/home/HeroSection";
import { StatsBar } from "@/components/home/StatsBar";
import { FeatureCards } from "@/components/home/FeatureCards";
import { CtaBanner } from "@/components/home/CtaBanner";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { Reveal } from "@/components/ui/Reveal";
import { CategorySidebar } from "@/components/category/CategorySidebar";
import { ErrorState } from "@/components/ui/Feedback";
import { RetryButton } from "@/components/ui/RetryButton";
import type { Category } from "@/types/category";
import type { RecipeSummary } from "@/types/recipe";

// Dựng HTML theo request để không giữ trang lỗi từ lúc build; dữ liệu API cache 1 giờ.
export const revalidate = 3600;

/**
 * Gọi API lấy dữ liệu trang chủ, KHÔNG để lỗi văng ra ngoài Server Component.
 * Lý do: nếu backend .NET chưa chạy (vd. lúc intern chỉ đang làm frontend), `fetch()` sẽ
 * ném lỗi "fetch failed" (ECONNREFUSED) — nếu không catch ở đây, cả trang chủ sẽ crash với
 * màn hình đỏ của Next.js thay vì hiển thị UI bình thường kèm thông báo nhẹ nhàng.
 */
async function getHomeData() {
    const [recipesResult, categoriesResult] = await Promise.allSettled([
      apiFetchPaged<RecipeSummary>("/recipes", {
        params: { page: 1, pageSize: 8, sort: "-createdAt" },
        next: { revalidate },
      }),
      apiFetch<Category[]>("/categories", { next: { revalidate } }),
    ]);
    return {
      recipes: recipesResult.status === "fulfilled" ? recipesResult.value.items : [],
      totalRecipes: recipesResult.status === "fulfilled" ? recipesResult.value.meta.total : 0,
      categories: categoriesResult.status === "fulfilled" ? categoriesResult.value : [],
      recipesUnavailable: recipesResult.status === "rejected",
      categoriesUnavailable: categoriesResult.status === "rejected",
    };
}

/** Trang chủ: hero + thống kê + công thức nổi bật + danh mục. */
export default async function HomePage() {
  await connection();
  const { recipes, totalRecipes, categories, recipesUnavailable, categoriesUnavailable } = await getHomeData();

  return (
    <div className="flex flex-col gap-16 pb-20 sm:gap-20">
      <HeroSection />

      {!recipesUnavailable && !categoriesUnavailable && <StatsBar recipeCount={totalRecipes} categoryCount={categories.length} />}

      <FeatureCards />

      <div className="container-page grid gap-8 lg:grid-cols-[260px_minmax(0,1fr)]">
      <CategorySidebar categories={categories} unavailable={categoriesUnavailable} />
      <section className="min-w-0">
        <Reveal>
          <SectionHeading
            eyebrow="Mới nhất"
            title="Công thức vừa được chia sẻ"
            description="Những món mới nhất từ cộng đồng — cập nhật mỗi giờ."
            moreHref="/recipes"
          />
        </Reveal>
        {recipesUnavailable ? <ErrorState message="Chưa tải được công thức mới. Vui lòng thử lại."><RetryButton /></ErrorState>
          : <RecipeGrid recipes={recipes} />}
      </section>
      </div>

      <section className="container-page">
        <Reveal>
          <SectionHeading
            eyebrow="Khám phá"
            title="Duyệt theo danh mục"
            description="Từ món khai vị đến tráng miệng — chọn nhóm món bạn đang thèm."
            moreHref="/categories"
          />
        </Reveal>
        {categoriesUnavailable ? <ErrorState message="Chưa tải được danh mục. Vui lòng thử lại."><RetryButton /></ErrorState>
          : <CategoryGrid categories={categories} />}
      </section>

      <CtaBanner />
    </div>
  );
}
