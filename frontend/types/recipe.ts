
/** Khớp enum RecipeDifficulty (mục 7.2): 1=Easy 2=Medium 3=Hard 4=Expert. */
export type RecipeDifficulty = "Easy" | "Medium" | "Hard" | "Expert";

/** Khớp enum RecipeStatus (mục 7.2): 0=Draft 1=Published 2=Archived. */
export type RecipeStatus = "Draft" | "Published" | "Archived";

/** RecipeNutrition — Owned Entity, tất cả field đều optional (mục 7.2). */
export interface RecipeNutrition {
  calories: number | null;
  protein: number | null;
  carbohydrates: number | null;
  fat: number | null;
  fiber: number | null;
  sodium: number | null;
}

/** Khớp bảng "RecipeSteps" (mục 7.3). */
export interface RecipeStep {
  id: string;
  stepNumber: number;
  title: string;
  description: string;
  timerMinutes: number | null;
  imageUrl: string | null;
}

/** Khớp bảng "RecipeIngredients" (mục 7.4). */
export interface RecipeIngredient {
  id: string;
  name: string;
  quantity: number | null;
  unit: string | null;
  notes: string | null;
  orderIndex: number;
}

/** Khớp bảng "RecipeImages" (mục 7.5). */
export interface RecipeImage {
  id: string;
  originalUrl: string;
  mediumUrl: string | null;
  thumbnailUrl: string | null;
  altText: string | null;
  isPrimary: boolean;
  orderIndex: number;
}

/**
 * Dạng rút gọn dùng cho danh sách (GET /recipes) — chỉ có 1 ảnh đại diện thay vì mảng đầy đủ,
 * để payload nhẹ hơn khi hiển thị dạng lưới (RecipeCard).
 */
export interface RecipeSummary {
  id: string;
  slug: string;
  title: string;
  description: string;
  prepTime: number;
  cookTime: number;
  servings: number;
  difficulty: RecipeDifficulty;
  status: RecipeStatus;
  /**
   * Backend trả TÊN danh mục/tác giả dạng chuỗi phẳng, không phải object lồng — xem
   * RecipeListItemDto.cs. Hệ quả: danh sách không có id tác giả (không lọc "của tôi" phía client
   * được) và không có slug danh mục (không tự dựng link /categories/{slug} được).
   */
  categoryName: string;
  authorDisplayName: string;
  primaryImageUrl: string | null;
  publishedAt: string | null;
}

/**
 * Chi tiết đầy đủ 1 recipe, dùng cho GET /recipes/{slug} (mục 8.3, FR-RCP-002).
 *
 * KHÔNG kế thừa RecipeSummary: bản chi tiết có `categoryId`/`authorId` và mảng `images` đầy đủ,
 * nhưng lại KHÔNG có `primaryImageUrl` — hai DTO khác nhau thật sự, gộp lại sẽ khai sai kiểu.
 */
export interface RecipeDetail {
  id: string;
  slug: string;
  title: string;
  description: string;
  instructions: string;
  prepTime: number;
  cookTime: number;
  servings: number;
  difficulty: RecipeDifficulty;
  status: RecipeStatus;
  categoryId: string;
  categoryName: string;
  authorId: string;
  authorDisplayName: string;
  publishedAt: string | null;
  nutrition: RecipeNutrition;
  steps: RecipeStep[];
  ingredients: RecipeIngredient[];
  images: RecipeImage[];
  /** RowVersion dạng base64 — bắt buộc gửi lại trong header If-Match khi PUT (FR-RCP-004, ETag pattern). */
  rowVersion: string;
}

/** Query params cho GET /recipes (mục 8.3, FR-SRCH-002/003/004). */
export interface RecipeListParams {
  page?: number;
  pageSize?: number;
  categoryId?: string;
  difficulty?: RecipeDifficulty;
  maxCookTime?: number;
  minServings?: number;
  sort?: string; // vd. "-createdAt", "title", "cookTime"
}

/** Query params cho GET /recipes/search (FR-SRCH-001). */
export interface RecipeSearchParams extends RecipeListParams {
  q: string;
}

/**
 * Body tạo recipe mới (FR-RCP-003) — trạng thái ban đầu luôn là Draft, server tự set.
 *
 * Tên field phải khớp record CreateRecipeRequest ở backend: `prepTime`/`cookTime` (KHÔNG phải
 * `prepTimeMinutes`), và `instructions` là bắt buộc — gửi thiếu thì model binder nhận null rồi
 * validator trả 422.
 */
export interface CreateRecipeInput {
  title: string;
  description: string;
  categoryId: string;
  prepTime: number;
  cookTime: number;
  servings: number;
  difficulty: RecipeDifficulty;
  instructions: string;
  nutrition?: Partial<RecipeNutrition>;
  /**
   * Khác hẳn POST /recipes/{id}/steps (nơi backend tự đánh số): ở đây CreateRecipeStepInput bắt
   * buộc có `stepNumber` và validator yêu cầu > 0. Thiếu nó thì nhận 0 rồi trả 422.
   */
  steps?: Array<Pick<RecipeStep, "stepNumber" | "title" | "description" | "timerMinutes">>;
  /** Tương tự, `orderIndex` là bắt buộc (đếm từ 0) chứ không optional như khi thêm lẻ. */
  ingredients?: Array<Pick<RecipeIngredient, "name" | "quantity" | "unit" | "notes" | "orderIndex">>;
}

/** Body cập nhật recipe (FR-RCP-004) — mọi field optional.
 * Tách riêng khỏi CreateRecipeInput vì frontend form dùng prepTimeMinutes/cookTimeMinutes,
 * còn API dùng prepTime/cookTime; đồng thời Update không cập nhật Steps/Ingredients. */
export interface UpdateRecipeInput {
  title?: string;
  description?: string;
  categoryId?: string;
  prepTime?: number;
  cookTime?: number;
  servings?: number;
  difficulty?: RecipeDifficulty;
  instructions?: string;
  nutrition?: Partial<RecipeNutrition>;
}

// ---------------------------------------------------------------------------
// Body cho các endpoint CRUD từng phần của công thức — phần việc của Quang.
// Tên field khớp đúng record request trong src/CulinaryBlog.API/Endpoints/RecipesEndpoints.cs;
// sai một chữ là backend nhận null rồi trả 422, nên đừng đổi tên tuỳ tiện.
// ---------------------------------------------------------------------------

/** POST /recipes/{id}/steps (FR-RCP-010). StepNumber do backend tự đánh, không gửi lên. */
export interface AddStepInput {
  title: string;
  description: string;
  timerMinutes?: number | null;
  imageUrl?: string | null;
}

/** PUT /recipes/{id}/steps/{stepId} (FR-RCP-010). */
export interface UpdateStepInput extends AddStepInput {
  stepNumber?: number | null;
}

/** POST /recipes/{id}/ingredients (FR-RCP-009). */
export interface AddIngredientInput {
  name: string;
  quantity?: number | null;
  unit?: string | null;
  notes?: string | null;
}

/** PUT /recipes/{id}/ingredients/{ingredientId} (FR-RCP-009). */
export interface UpdateIngredientInput extends AddIngredientInput {
  orderIndex?: number | null;
}

/** PUT /recipes/{id}/images/{imageId} (FR-RCP-008). Không gửi field nào = giữ nguyên field đó. */
export interface UpdateImageInput {
  altText?: string | null;
  isPrimary?: boolean | null;
  orderIndex?: number | null;
}
