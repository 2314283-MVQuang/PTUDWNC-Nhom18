import Link from "next/link";
import { apiFetch, apiFetchPaged, apiErrorMessage } from "@/lib/api-client";
import { readSearchFilters, readSearchPage, searchFilterParams, searchHref, validateSearchFilters, EMPTY_SEARCH_FILTERS } from "@/lib/search-filters";
import { Pagination } from "@/components/ui/Pagination";
import { PageHero } from "@/components/layout/PageHero";
import { EmptyState, ErrorState } from "@/components/ui/Feedback";
import { RetryButton } from "@/components/ui/RetryButton";
import { SearchFilters } from "@/components/search/SearchFilters";
import { SearchResults } from "@/components/search/SearchResults";
import type { SearchRecipe } from "@/types/search";
import type { Category } from "@/types/category";

export const dynamic = "force-dynamic";

export default async function SearchPage({ searchParams }: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const draft = readSearchFilters(params);
  const page = readSearchPage(params);
  const issues = validateSearchFilters(draft);
  const hasQuery = draft.q.length >= 2;
  const [categoryResult, recipeResult] = await Promise.allSettled([
    apiFetch<Category[]>("/categories", { cache: "no-store" }),
    hasQuery && !issues.length
      ? apiFetchPaged<SearchRecipe>("/recipes/search", { params: { ...searchFilterParams(draft), page, pageSize: 12 }, cache: "no-store" })
      : Promise.resolve(null),
  ]);
  const result = recipeResult.status === "fulfilled" ? recipeResult.value : null;
  const recipes = result?.items ?? null;
  const categories = categoryResult.status === "fulfilled" ? categoryResult.value : [];
  const activeFilters = Object.entries(searchFilterParams(draft)).filter(([key]) => key !== "q" && key !== "sort").length;

  return <>
    <PageHero title={hasQuery ? `Kết quả cho “${draft.q}”` : "Tìm kiếm công thức"}
      description="Tìm món ngon theo danh mục, độ khó và thời gian bạn có." emojis={["🔎", "🍲"]} />
    <div className="container-page grid gap-6 pb-20 pt-8 lg:grid-cols-[280px_minmax(0,1fr)]">
      <SearchFilters initial={draft} categories={categories} categoriesUnavailable={categoryResult.status === "rejected"} />
      <section aria-label="Kết quả tìm kiếm" className="min-w-0 space-y-5">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-brand-100 pb-4">
          <p className="text-sm text-neutral-600">{result ? `${result.meta.total} công thức phù hợp${result.meta.totalPages > 1 ? ` · trang ${result.meta.page}/${result.meta.totalPages}` : ""}` : "Khám phá món ngon mỗi ngày"}{activeFilters > 0 && ` · ${activeFilters} điều kiện đang áp dụng`}</p>
          {activeFilters > 0 && <Link className="text-sm font-medium text-brand-700 underline underline-offset-4" href={searchHref({ ...EMPTY_SEARCH_FILTERS, q: draft.q, sort: draft.sort })}>Xóa bộ lọc đang áp dụng</Link>}
        </div>
        {issues.length > 0 ? <ErrorState title="Bộ lọc chưa hợp lệ" message={issues.join(" ")} />
          : !hasQuery ? <EmptyState title="Bạn muốn nấu món gì?" message="Nhập ít nhất 2 ký tự trong bộ lọc hoặc thanh tìm kiếm để bắt đầu. Bạn có thể thêm danh mục, độ khó, khoảng thời gian và số khẩu phần." />
          : recipeResult.status === "rejected" ? <ErrorState message={apiErrorMessage(recipeResult.reason, "Không thể kết nối để tải công thức. Vui lòng thử lại sau.")}><RetryButton /></ErrorState>
          : recipes?.length === 0 ? <EmptyState title="Chưa tìm thấy công thức phù hợp" message="Thử từ khóa khác hoặc nới rộng khoảng thời gian, số khẩu phần, danh mục và độ khó." />
          : recipes && <SearchResults recipes={recipes} />}
        {/* FR-SRCH-004: giữ nguyên từ khóa, bộ lọc và sắp xếp khi chuyển trang. */}
        {result && <Pagination meta={result.meta} buildHref={p => searchHref(draft, p)} />}
      </section>
    </div>
  </>;
}
