"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch, apiFetchPaged } from "@/lib/api-client";
import { useCurrentUser } from "./useAuth";
import type { UserListParams, UserSummary } from "@/types/user";

export const userKeys = {
  all: ["users"] as const,
  list: (params: UserListParams) => ["users", "list", params] as const,
};

/** Tuần 9 (Quang) — GET /users: danh sách người dùng cho Admin (tìm kiếm, lọc vai trò/trạng thái, phân trang). */
export function useUsers(params: UserListParams) {
  const { accessToken, isAdmin } = useCurrentUser();

  return useQuery({
    queryKey: userKeys.list(params),
    queryFn: () =>
      apiFetchPaged<UserSummary>("/users", {
        params: { ...params, q: params.q?.trim() || undefined },
        token: accessToken,
      }),
    enabled: !!accessToken && isAdmin,
    placeholderData: keepPreviousData,
  });
}

/** PATCH /users/{id}/status — khoá / mở khoá tài khoản (backend chặn Admin tự khoá chính mình). */
export function useSetUserStatus() {
  const queryClient = useQueryClient();
  const { accessToken } = useCurrentUser();

  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      apiFetch<UserSummary>(`/users/${id}/status`, { method: "PATCH", body: { isActive }, token: accessToken }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: userKeys.all }),
  });
}
