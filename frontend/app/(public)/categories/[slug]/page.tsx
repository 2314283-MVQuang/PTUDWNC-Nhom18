import Link from "next/link";
import { notFound } from "next/navigation";
import { apiFetch, ApiError } from "@/lib/api-client";
import { RecipeGrid } from "@/components/recipe/RecipeGrid";
import { Pagination } from "@/components/ui/Pagination";
import { EmptyState } from "@/components/ui/Feedback";
import type { CategoryDetail } from "@/types/category";

// Mục 9: "/categories/[slug]" — ISR revalidate=600 (10 phút). Backend cache thêm 5 phút
// (policy "CategoryDetail") và tự xoá khi danh mục/công thức thay đổi.
export const revalidate = 600;

const PAGE_SIZE = 12;

interface CategoryPageProps {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ page?: string }>;
}

/** ?page= lạ (0, âm, chữ) quay về trang 1 thay vì gửi lên backend rồi nhận 422. */
function readPage(value: string | undefined): number {
  return value && /^\d+$/.test(value) && Number(value) >= 1 ? Number(value) : 1;
}

export default async function CategoryDetailPage({ params, searchParams }: CategoryPageProps) {
  const { slug } = await params;
  const page = readPage((await searchParams).page);

  let detail: CategoryDetail;
  try {
    // FR-CAT-002: 1 request trả CẢ thông tin danh mục lẫn trang công thức — không gọi thêm
    // GET /recipes?categoryId= như trước (khi đó data bị đọc nhầm thành Category nên trang trống).
    detail = await apiFetch<CategoryDetail>(`/categories/${encodeURIComponent(slug)}`, {
      params: { page, pageSize: PAGE_SIZE },
      next: { revalidate },
    });
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  }

  const { category, recipes } = detail;
  const meta = {
    page: recipes.page,
    pageSize: recipes.pageSize,
    total: recipes.totalCount,
    totalPages: recipes.totalPages,
    hasNextPage: recipes.hasNextPage,
    hasPreviousPage: recipes.hasPreviousPage,
  };

  return (
    <div className="container-page flex flex-col gap-6 py-10">
      <nav aria-label="Breadcrumb" className="text-sm text-neutral-500">
        <Link href="/categories" className="hover:text-brand-700 hover:underline">Danh mục</Link>
        <span aria-hidden="true"> / </span>
        <span className="text-neutral-700">{category.name}</span>
      </nav>
      <div>
        <h1 className="text-2xl font-bold text-neutral-900">{category.name}</h1>
        {category.description && <p className="mt-2 text-neutral-600">{category.description}</p>}
        <p className="mt-1 text-sm text-neutral-500">{category.recipeCount} công thức đã xuất bản</p>
      </div>
      {recipes.items.length === 0 ? (
        <EmptyState
          title={page > 1 ? "Trang này không có công thức" : "Danh mục chưa có công thức"}
          message={page > 1 ? "Hãy quay lại trang đầu của danh mục." : "Hãy quay lại sau khi có công thức được xuất bản."}
        />
      ) : (
        <RecipeGrid recipes={recipes.items} />
      )}
      <Pagination meta={meta} buildHref={(p) => `/categories/${slug}?page=${p}`} />
    </div>
  );
}
