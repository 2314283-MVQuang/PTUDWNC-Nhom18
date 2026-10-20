import type { RecipeDifficulty, RecipeStatus } from "./recipe";

/** Đúng DTO GET /recipes/search; chưa có ảnh, tác giả hay meta phân trang. */
export interface SearchRecipe {
  id: string;
  slug: string;
  title: string;
  description: string;
  prepTime: number;
  cookTime: number;
  servings: number;
  difficulty: RecipeDifficulty;
  status: RecipeStatus;
  publishedAt: string | null;
}

/** Giữ dạng chuỗi để phân biệt bỏ trống với số 0. */
export interface SearchFilterDraft {
  q: string;
  categoryId: string;
  difficulty: string;
  minPrepTime: string;
  maxPrepTime: string;
  minCookTime: string;
  maxCookTime: string;
  minServings: string;
}
