"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api-client";
import { useCurrentUser } from "./useAuth";
import { useSession } from "next-auth/react";
import type { UpdateProfileInput, UserProfile } from "@/types/user";

export const profileKeys = {
  me: (userId?: string) => ["profile", "me", userId ?? "anonymous"] as const,
};

/** FR-AUTH-006: GET /auth/me — hồ sơ đầy đủ của user đang đăng nhập (bio/avatar không có trong session JWT). */
export function useMyProfile() {
  const { accessToken, isAuthenticated, isDemo, user } = useCurrentUser();

  return useQuery({
    queryKey: profileKeys.me(user?.id),
    queryFn: () => apiFetch<UserProfile>("/auth/me", { token: accessToken }),
    enabled: isAuthenticated && !isDemo,
  });
}

/**
 * FR-AUTH-007: PATCH /auth/me — chỉ đổi displayName/bio/avatarUrl.
 * Cache theo user; session được nạp lại từ backend sau khi lưu để Header đổi ngay.
 */
export function useUpdateProfile() {
  const queryClient = useQueryClient();
  const { accessToken, user } = useCurrentUser();
  const { update } = useSession();

  return useMutation({
    mutationFn: (input: UpdateProfileInput) =>
      apiFetch<UserProfile>("/auth/me", { method: "PATCH", body: input, token: accessToken }),
    onSuccess: async (data) => {
      queryClient.setQueryData(profileKeys.me(user?.id), data);
      // Lưu hồ sơ đã thành công: lỗi đồng bộ Header không được báo nhầm là lỗi lưu.
      // Auth.js beta.25 chỉ phát trigger="update" khi truyền data; object rỗng không chứa quyền/token.
      try { await update({}); } catch { /* Có thể đồng bộ lại ở lần lưu/đăng nhập sau. */ }
    },
  });
}
