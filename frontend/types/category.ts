import type { RecipeSummary } from "./recipe";

/** Khớp bảng "Categories" (mục 7.6) + recipeCount trả kèm ở GET /categories (mục 8.2). */
export interface Category {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  imageUrl: string | null;
  recipeCount: number;
}

/** Body tạo/sửa category (FR-CAT-003, FR-CAT-004) — Admin only. */
export interface CategoryInput {
  name: string;
  description?: string;
  imageUrl?: string;
}

/**
 * GET /categories/{slug}?page=&pageSize= (FR-CAT-002) — khớp CategoryDetailDto ở backend:
 * thông tin danh mục + danh sách công thức Published (kèm bản nháp của chính người xem) có phân
 * trang. `recipes` là PagedResult serialize thẳng nên tên field là totalCount (KHÁC `meta.total`
 * của các endpoint danh sách dùng envelope { data, meta }).
 */
export interface CategoryDetail {
  category: Category;
  recipes: {
    items: RecipeSummary[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
  };
}
