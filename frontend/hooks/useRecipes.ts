"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch, apiFetchPaged } from "@/lib/api-client";
import { useCurrentUser } from "./useAuth";
import { categoryKeys } from "./useCategories";
import type {
  CreateRecipeInput,
  RecipeDetail,
  RecipeListParams,
  RecipeSearchParams,
  RecipeSummary,
  UpdateRecipeInput,
} from "@/types/recipe";

export const recipeKeys = {
  all: ["recipes"] as const,
  list: (params: RecipeListParams, userId = "guest") => ["recipes", "list", userId, params] as const,
  detail: (slug: string, userId = "guest") => ["recipes", "detail", userId, slug] as const,
  search: (params: RecipeSearchParams) => ["recipes", "search", params] as const,
};

/** FR-RCP-001: GET /recipes — danh sách có filter/sort/phân trang (mục 8.3). */
export function useRecipes(params: RecipeListParams = {}) {
  const { accessToken, user, isLoading } = useCurrentUser();

  return useQuery({
    queryKey: recipeKeys.list(params, accessToken ? user?.id : undefined),
    queryFn: () =>
      apiFetchPaged<RecipeSummary>("/recipes", {
        params: { page: 1, pageSize: 12, sort: "-createdAt", ...params },
        token: accessToken,
      }),
    staleTime: 15_000,
    enabled: !isLoading,
  });
}

/** FR-RCP-002: GET /recipes/{slug} — chi tiết đầy đủ, dùng ở trang /recipes/[slug] và form edit. */
export function useRecipe(slug: string | undefined) {
  const { accessToken, user, isLoading } = useCurrentUser();

  return useQuery({
    queryKey: recipeKeys.detail(slug ?? "", accessToken ? user?.id : undefined),
    queryFn: () => apiFetch<RecipeDetail>(`/recipes/${slug}`, { token: accessToken }),
    enabled: !!slug && !isLoading,
  });
}

/** FR-SRCH-001: GET /recipes/search?q= — Full-Text Search tiếng Việt. */
export function useSearchRecipes(params: RecipeSearchParams) {
  return useQuery({
    queryKey: recipeKeys.search(params),
    // Trải `...params` ra object mới thay vì truyền thẳng: apiFetchPaged nhận kiểu
    // Record<string, ...>, mà interface RecipeSearchParams không tự có index signature nên
    // TypeScript từ chối gán trực tiếp. Trải ra cũng là chỗ đặt giá trị mặc định cho phân trang.
    queryFn: () =>
      apiFetchPaged<RecipeSummary>("/recipes/search", {
        params: { page: 1, pageSize: 12, ...params },
      }),
    enabled: params.q.trim().length >= 2, // backend trả 422 nếu q < 2 ký tự, chặn sớm ở FE
    staleTime: 5_000,
  });
}

/** FR-RCP-003: POST /recipes — tạo mới, trạng thái ban đầu luôn Draft. */
export function useCreateRecipe() {
  const queryClient = useQueryClient();
  const { accessToken } = useCurrentUser();

  return useMutation({
    mutationFn: (input: CreateRecipeInput) =>
      apiFetch<RecipeDetail>("/recipes", { method: "POST", body: input, token: accessToken }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: recipeKeys.all }),
  });
}

/** FR-RCP-004: PUT /recipes/{id} — cần gửi RowVersion qua header If-Match (optimistic concurrency). */
export function useUpdateRecipe() {
  const queryClient = useQueryClient();
  const { accessToken } = useCurrentUser();

  return useMutation({
    mutationFn: ({
      id,
      input,
      rowVersion,
    }: {
      id: string;
      input: UpdateRecipeInput;
      rowVersion: string;
    }) =>
      apiFetch<RecipeDetail>(`/recipes/${id}`, {
        method: "PUT",
        body: input,
        token: accessToken,
        ifMatch: rowVersion,
      }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: recipeKeys.all }),
      queryClient.invalidateQueries({ queryKey: categoryKeys.all }),
    ]),
  });
}

/** FR-RCP-005: PATCH /recipes/{id}/publish — 400 nếu thiếu step hoặc ingredient. */
export function usePublishRecipe() {
  const queryClient = useQueryClient();
  const { accessToken } = useCurrentUser();

  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<RecipeDetail>(`/recipes/${id}/publish`, { method: "PATCH", token: accessToken }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: recipeKeys.all }),
      queryClient.invalidateQueries({ queryKey: categoryKeys.all }),
    ]),
  });
}

/** FR-RCP-006: Hủy xuất bản bằng cách lưu trữ, giữ nguyên dữ liệu. */
export function useUnpublishRecipe() {
  const queryClient = useQueryClient();
  const { accessToken } = useCurrentUser();

  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<RecipeDetail>(`/recipes/${id}/archive`, { method: "PATCH", token: accessToken }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: recipeKeys.all }),
      queryClient.invalidateQueries({ queryKey: categoryKeys.all }),
    ]),
  });
}

/** FR-RCP-006: PATCH /recipes/{id}/archive — ẩn công khai nhưng không xoá dữ liệu. */
export function useArchiveRecipe() {
  const queryClient = useQueryClient();
  const { accessToken } = useCurrentUser();

  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<RecipeDetail>(`/recipes/${id}/archive`, { method: "PATCH", token: accessToken }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: recipeKeys.all }),
      queryClient.invalidateQueries({ queryKey: categoryKeys.all }),
    ]),
  });
}

/** FR-RCP-006: Khôi phục trạng thái trước khi lưu trữ. */
export function useUnarchiveRecipe() {
  const queryClient = useQueryClient();
  const { accessToken } = useCurrentUser();

  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<RecipeDetail>(`/recipes/${id}/unarchive`, { method: "PATCH", token: accessToken }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: recipeKeys.all }),
      queryClient.invalidateQueries({ queryKey: categoryKeys.all }),
    ]),
  });
}

/** FR-RCP-007: Xóa mềm; backend dọn dữ liệu sau 30 ngày. */
export function useDeleteRecipe() {
  const queryClient = useQueryClient();
  const { accessToken } = useCurrentUser();

  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/recipes/${id}`, { method: "DELETE", token: accessToken }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: recipeKeys.all }),
      queryClient.invalidateQueries({ queryKey: categoryKeys.all }),
    ]),
  });
}
