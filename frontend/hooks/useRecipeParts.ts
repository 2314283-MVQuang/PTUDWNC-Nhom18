"use client";

import { useInfiniteQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api-client";
import { copyImageFromUrl, uploadImageFile, uploadRecipeImage } from "@/lib/recipe-images";
import type { MyImage, MyImagePage } from "@/types/media";
import { useCurrentUser } from "./useAuth";
import { recipeKeys } from "./useRecipes";
import type {
  AddIngredientInput,
  AddStepInput,
  RecipeImage,
  RecipeIngredient,
  RecipeStep,
  UpdateIngredientInput,
  UpdateStepInput,
} from "@/types/recipe";

/**
 * FR-RCP-008/009/010 — sửa từng ảnh / nguyên liệu / bước riêng lẻ.
 *
 * Tách khỏi useRecipes.ts vì ở đó là các thao tác trên NGUYÊN công thức (tạo, publish, xoá),
 * còn đây là thao tác trên các phần con. Mọi hook đều nhận `recipeId` ngay lúc gọi hook, vì
 * component dùng chúng (các editor trong trang sửa công thức) luôn biết sẵn công thức nào.
 *
 * Sau mỗi thay đổi đều invalidate `recipeKeys.all`. Key đó là tiền tố ["recipes"] nên React Query
 * làm mới cả danh sách lẫn chi tiết — cần thiết vì xoá một bước khiến backend ĐÁNH SỐ LẠI toàn bộ
 * các bước còn lại, dữ liệu cũ trong cache sẽ sai ngay lập tức.
 */
function useRecipePartMutation() {
  const queryClient = useQueryClient();
  const { accessToken } = useCurrentUser();

  return {
    accessToken,
    invalidate: () => queryClient.invalidateQueries({ queryKey: recipeKeys.all }),
  };
}

// ---------------------------------------------------------------------------
// FR-RCP-010 — Các bước thực hiện
// ---------------------------------------------------------------------------

export function useAddStep(recipeId: string) {
  const { accessToken, invalidate } = useRecipePartMutation();

  return useMutation({
    mutationFn: (input: AddStepInput) =>
      apiFetch<RecipeStep>(`/recipes/${recipeId}/steps`, {
        method: "POST",
        body: input,
        token: accessToken,
      }),
    onSuccess: invalidate,
  });
}

export function useUpdateStep(recipeId: string) {
  const { accessToken, invalidate } = useRecipePartMutation();

  return useMutation({
    mutationFn: ({ stepId, input }: { stepId: string; input: UpdateStepInput }) =>
      apiFetch<RecipeStep>(`/recipes/${recipeId}/steps/${stepId}`, {
        method: "PUT",
        body: input,
        token: accessToken,
      }),
    onSuccess: invalidate,
  });
}

/** Xoá xong backend tự đánh số lại các bước còn lại cho liên tục 1,2,3… (FR-RCP-010). */
export function useDeleteStep(recipeId: string) {
  const { accessToken, invalidate } = useRecipePartMutation();

  return useMutation({
    mutationFn: (stepId: string) =>
      apiFetch<void>(`/recipes/${recipeId}/steps/${stepId}`, {
        method: "DELETE",
        token: accessToken,
      }),
    onSuccess: invalidate,
  });
}

// ---------------------------------------------------------------------------
// FR-RCP-009 — Nguyên liệu
// ---------------------------------------------------------------------------

export function useAddIngredient(recipeId: string) {
  const { accessToken, invalidate } = useRecipePartMutation();

  return useMutation({
    mutationFn: (input: AddIngredientInput) =>
      apiFetch<RecipeIngredient>(`/recipes/${recipeId}/ingredients`, {
        method: "POST",
        body: input,
        token: accessToken,
      }),
    onSuccess: invalidate,
  });
}

export function useUpdateIngredient(recipeId: string) {
  const { accessToken, invalidate } = useRecipePartMutation();

  return useMutation({
    mutationFn: ({ ingredientId, input }: { ingredientId: string; input: UpdateIngredientInput }) =>
      apiFetch<RecipeIngredient>(`/recipes/${recipeId}/ingredients/${ingredientId}`, {
        method: "PUT",
        body: input,
        token: accessToken,
      }),
    onSuccess: invalidate,
  });
}

export function useDeleteIngredient(recipeId: string) {
  const { accessToken, invalidate } = useRecipePartMutation();

  return useMutation({
    mutationFn: (ingredientId: string) =>
      apiFetch<void>(`/recipes/${recipeId}/ingredients/${ingredientId}`, {
        method: "DELETE",
        token: accessToken,
      }),
    onSuccess: invalidate,
  });
}

// ---------------------------------------------------------------------------
// FR-RCP-008 — Ảnh
// ---------------------------------------------------------------------------

/**
 * FR-RCP-008: upload qua presigned URL MinIO (xem lib/recipe-images.ts). Lỗi trả về giữ nguyên
 * thông báo của backend (sai loại/quá dung lượng) để component hiển thị cho người dùng.
 */
export function useUploadImage(recipeId: string) {
  const { accessToken, invalidate } = useRecipePartMutation();

  return useMutation({
    mutationFn: ({ file, altText }: { file: File; altText?: string }) =>
      uploadRecipeImage(recipeId, file, { token: accessToken, altText }),
    onSuccess: invalidate,
  });
}

/**
 * PATCH /recipes/{id}/images/{imageId}/primary — đặt ảnh đại diện. Backend tự bỏ cờ của ảnh đại
 * diện cũ trong cùng transaction (unique partial index chỉ cho 1 ảnh đại diện mỗi công thức).
 */
export function useSetPrimaryImage(recipeId: string) {
  const { accessToken, invalidate } = useRecipePartMutation();

  return useMutation({
    mutationFn: (imageId: string) =>
      apiFetch<RecipeImage>(`/recipes/${recipeId}/images/${imageId}/primary`, {
        method: "PATCH",
        token: accessToken,
      }),
    onSuccess: invalidate,
  });
}

/** Xoá ảnh đại diện thì backend tự đôn ảnh còn lại đầu tiên lên làm đại diện. */
export function useDeleteImage(recipeId: string) {
  const { accessToken, invalidate } = useRecipePartMutation();

  return useMutation({
    mutationFn: (imageId: string) =>
      apiFetch<void>(`/recipes/${recipeId}/images/${imageId}`, {
        method: "DELETE",
        token: accessToken,
      }),
    onSuccess: invalidate,
  });
}

/**
 * Tuần 10: tải một ảnh lên MinIO và trả về URL — dùng cho ảnh minh hoạ từng bước (FR-RCP-010) và
 * ảnh danh mục (FR-CAT-003/004). Nhận file từ máy/camera/kéo thả, hoặc một ảnh trong "Thư viện ảnh
 * của tôi" (khi đó tải lên bản sao mới). URL trả về được gửi kèm khi lưu bước / danh mục.
 */
export function useUploadImageFile() {
  const { accessToken } = useCurrentUser();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (source: File | MyImage) =>
      source instanceof File
        ? uploadImageFile(source, { token: accessToken })
        : copyImageFromUrl(source, { token: accessToken }),
    // Ảnh mới tải lên xuất hiện ngay trong thư viện.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: myImageKeys.all }),
  });
}

export const myImageKeys = { all: ["files", "mine"] as const };

/** GET /files/mine — "Thư viện ảnh của tôi", mới nhất trước, 24 ảnh mỗi lần "Xem thêm". */
export function useMyImages(enabled: boolean) {
  const { accessToken } = useCurrentUser();

  return useInfiniteQuery({
    queryKey: myImageKeys.all,
    enabled: enabled && !!accessToken,
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      apiFetch<MyImagePage>("/files/mine", { params: { page: pageParam, pageSize: 24 }, token: accessToken }),
    getNextPageParam: (last) => (last.hasNextPage ? last.page + 1 : undefined),
  });
}
