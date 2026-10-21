"use client";

import { useMutation } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api-client";
import { normalizeAuthToken } from "@/lib/validations/auth.schema";
import { useCurrentUser } from "./useAuth";

/**
 * Auth nâng cao — 4 endpoint backend Tuần 3 (Quang), nay có giao diện:
 *   POST /auth/forgot-password  { email }                       → 204 (luôn 204, không lộ email)
 *   POST /auth/reset-password   { email, token, newPassword }   → 204
 *   POST /auth/confirm-email    { userId, token }               → 204
 *   POST /auth/change-password  { currentPassword, newPassword } → 204, cần đăng nhập
 * Nhóm /auth bị giới hạn 10 request/phút/IP; lỗi 429 hiển thị qua apiErrorMessage.
 */

export function useForgotPassword() {
  return useMutation({
    mutationFn: (email: string) =>
      apiFetch<void>("/auth/forgot-password", { method: "POST", body: { email: email.trim() } }),
  });
}

export function useResetPassword() {
  return useMutation({
    mutationFn: (input: { email: string; token: string; newPassword: string }) =>
      apiFetch<void>("/auth/reset-password", {
        method: "POST",
        body: { email: input.email.trim(), token: normalizeAuthToken(input.token), newPassword: input.newPassword },
      }),
  });
}

export function useConfirmEmail() {
  return useMutation({
    mutationFn: (input: { userId: string; token: string }) =>
      apiFetch<void>("/auth/confirm-email", {
        method: "POST",
        body: { userId: input.userId.trim(), token: normalizeAuthToken(input.token) },
      }),
  });
}

export function useChangePassword() {
  const { accessToken } = useCurrentUser();

  return useMutation({
    mutationFn: (input: { currentPassword: string; newPassword: string }) =>
      apiFetch<void>("/auth/change-password", { method: "POST", body: input, token: accessToken }),
  });
}
