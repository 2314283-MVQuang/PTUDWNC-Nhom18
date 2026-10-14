/**
 * Type dùng chung cho toàn bộ API response, khớp với mục 8 trong tài liệu đặc tả:
 *
 *   Response thành công: { "data": {...}, "meta": { page, pageSize, total, totalPages } }
 *   Response lỗi (RFC 7807): { type, title, status, detail, errors: {} }
 */

/** Bọc quanh mọi response thành công từ API. `meta` chỉ có khi endpoint trả danh sách phân trang. */
export interface ApiEnvelope<T> {
  data: T;
  meta?: PageMeta;
}

export interface PageMeta {
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

/** Kết quả phân trang dùng ở tầng UI sau khi đã "bóc" ApiEnvelope ra (xem lib/api-client.ts). */
export interface PagedResult<T> {
  items: T[];
  meta: PageMeta;
}

/**
 * RFC 7807 Problem Details — format lỗi chuẩn của toàn bộ API (CONS-005, mục 10).
 * `errors` xuất hiện khi lỗi là VALIDATION_ERROR (422), key là tên field.
 */
export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/** Danh sách mã lỗi nghiệp vụ (mục 10.2) — dùng để so sánh `problem.type` trong code, tránh gõ nhầm chuỗi. */
export const ApiErrorCode = {
  AuthEmailExists: "AUTH_EMAIL_EXISTS",
  AuthInvalidCredentials: "AUTH_INVALID_CREDENTIALS",
  AuthTokenExpired: "AUTH_TOKEN_EXPIRED",
  AuthTokenInvalid: "AUTH_TOKEN_INVALID",
  AuthRefreshTokenExpired: "AUTH_REFRESH_TOKEN_EXPIRED",
  AuthRefreshTokenRevoked: "AUTH_REFRESH_TOKEN_REVOKED",
  AuthGoogleTokenInvalid: "AUTH_GOOGLE_TOKEN_INVALID",
  AuthAccountDisabled: "AUTH_ACCOUNT_DISABLED",
  RecipeNotFound: "RECIPE_NOT_FOUND",
  RecipeSlugExists: "RECIPE_SLUG_EXISTS",
  RecipePublishIncomplete: "RECIPE_PUBLISH_INCOMPLETE",
  RecipeForbidden: "RECIPE_FORBIDDEN",
  RecipeConcurrencyConflict: "RECIPE_CONCURRENCY_CONFLICT",
  CategoryNotFound: "CATEGORY_NOT_FOUND",
  CategoryNameExists: "CATEGORY_NAME_EXISTS",
  CategoryDeleteHasRecipes: "CATEGORY_DELETE_HAS_RECIPES",
  FileSizeExceeded: "FILE_SIZE_EXCEEDED",
  FileMimeInvalid: "FILE_MIME_INVALID",
  ValidationError: "VALIDATION_ERROR",
  RateLimitExceeded: "RATE_LIMIT_EXCEEDED",
} as const;
