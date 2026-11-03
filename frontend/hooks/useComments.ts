"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch, apiFetchPaged } from "@/lib/api-client";
import { useCurrentUser } from "./useAuth";
import type { RecipeComment } from "@/types/comment";

export const commentKeys = {
  all: ["comments"] as const,
  recipe: (recipeId: string) => ["comments", recipeId] as const,
  page: (recipeId: string, page: number, userId?: string) => ["comments", recipeId, page, userId ?? "guest"] as const,
};

export const COMMENTS_PAGE_SIZE = 10;

/** FR-CMT-001: GET /recipes/{id}/comments. Gửi token (nếu có) để backend tính canEdit/canDelete. */
export function useRecipeComments(recipeId: string, page: number) {
  const { accessToken, isLoading, user } = useCurrentUser();
  return useQuery({
    queryKey: commentKeys.page(recipeId, page, user?.email ?? undefined),
    queryFn: () =>
      apiFetchPaged<RecipeComment>(`/recipes/${recipeId}/comments`, {
        params: { page, pageSize: COMMENTS_PAGE_SIZE },
        token: accessToken,
      }),
    enabled: !isLoading,
    placeholderData: keepPreviousData,
  });
}

function useCommentMutation<TVars, TResult>(fn: (vars: TVars, token?: string) => Promise<TResult>, recipeId: string) {
  const queryClient = useQueryClient();
  const { accessToken } = useCurrentUser();
  return useMutation({
    mutationFn: (vars: TVars) => fn(vars, accessToken),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: commentKeys.recipe(recipeId) }),
  });
}

/** FR-CMT-002: POST /recipes/{id}/comments. */
export function useAddComment(recipeId: string) {
  return useCommentMutation(
    (content: string, token) =>
      apiFetch<RecipeComment>(`/recipes/${recipeId}/comments`, { method: "POST", body: { content }, token }),
    recipeId,
  );
}

/** FR-CMT-003: PUT /comments/{id} — chỉ người viết. */
export function useUpdateComment(recipeId: string) {
  return useCommentMutation(
    ({ id, content }: { id: string; content: string }, token) =>
      apiFetch<RecipeComment>(`/comments/${id}`, { method: "PUT", body: { content }, token }),
    recipeId,
  );
}

/** FR-CMT-004: DELETE /comments/{id} — người viết, tác giả công thức hoặc Admin. */
export function useDeleteComment(recipeId: string) {
  return useCommentMutation(
    (id: string, token) => apiFetch<void>(`/comments/${id}`, { method: "DELETE", token }),
    recipeId,
  );
}
