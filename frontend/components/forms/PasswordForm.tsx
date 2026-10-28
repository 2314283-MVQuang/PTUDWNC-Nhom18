"use client";

import Link from "next/link";
import { useState } from "react";
import { apiFetch, apiErrorMessage } from "@/lib/api-client";
import { changePasswordSchema, forgotPasswordSchema, resetPasswordSchema } from "@/lib/validations/auth.schema";
import { useCurrentUser } from "@/hooks/useAuth";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";

export function PasswordForm({ mode, email = "", token = "" }: {
  mode: "forgot" | "reset" | "change"; email?: string; token?: string;
}) {
  const { accessToken, isDemo, isLoading } = useCurrentUser();
  const [address, setAddress] = useState(email);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  if (mode === "change" && isDemo) return <p role="status">Tài khoản demo không hỗ trợ đổi mật khẩu. Hãy đăng nhập bằng tài khoản hệ thống.</p>;
  if (mode === "reset" && (!email || !token)) return <div role="alert" className="space-y-3">
    <p>Liên kết đặt lại mật khẩu không đầy đủ. Hãy yêu cầu một liên kết mới.</p>
    <Link href="/forgot-password" className="text-brand-700 underline">Yêu cầu đặt lại mật khẩu</Link>
  </div>;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setError(""); setSuccess("");
    const values = { email: address, token, currentPassword, newPassword, confirmPassword };
    const schema = mode === "forgot" ? forgotPasswordSchema : mode === "reset" ? resetPasswordSchema : changePasswordSchema;
    const checked = schema.safeParse(values);
    if (!checked.success) { setError(checked.error.issues[0]?.message ?? "Dữ liệu chưa hợp lệ."); return; }
    if (mode === "change" && !accessToken) { setError("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại."); return; }
    const body = mode === "forgot" ? { email: address.trim() }
      : mode === "reset" ? { email: address.trim(), token, newPassword }
      : { currentPassword, newPassword };
    setPending(true);
    try {
      await apiFetch<void>(`/auth/${mode === "forgot" ? "forgot-password" : mode === "reset" ? "reset-password" : "change-password"}`, {
        method: "POST", body, token: mode === "change" ? accessToken : undefined,
      });
      setSuccess(mode === "forgot"
        ? "Nếu email đã đăng ký, hướng dẫn đặt lại mật khẩu sẽ được gửi đến hộp thư."
        : mode === "reset" ? "Đã đặt lại mật khẩu. Có thể đăng nhập bằng mật khẩu mới."
        : "Đã đổi mật khẩu thành công.");
      setCurrentPassword(""); setNewPassword(""); setConfirmPassword("");
    } catch (failure) {
      setError(apiErrorMessage(failure, "Chưa thực hiện được yêu cầu. Vui lòng thử lại."));
    } finally { setPending(false); }
  }

  return <form onSubmit={submit} className="flex flex-col gap-4">
    {mode !== "change" && <Input name="email" label="Email" type="email" autoComplete="email" required
      value={address} readOnly={mode === "reset"} onChange={event => setAddress(event.target.value)} />}
    {mode === "change" && <Input name="currentPassword" label="Mật khẩu hiện tại" type="password" autoComplete="current-password" required
      value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} />}
    {mode !== "forgot" && <>
      <p className="text-sm text-neutral-600">Mật khẩu mới cần ít nhất 8 ký tự, có chữ hoa, chữ số và ký tự đặc biệt.</p>
      <Input name="newPassword" label="Mật khẩu mới" type="password" autoComplete="new-password" required
        value={newPassword} onChange={event => setNewPassword(event.target.value)} />
      <Input name="confirmPassword" label="Xác nhận mật khẩu mới" type="password" autoComplete="new-password" required
        value={confirmPassword} onChange={event => setConfirmPassword(event.target.value)} />
    </>}
    {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-800">{error}</p>}
    {success && <p role="status" className="rounded-lg bg-green-50 p-3 text-sm text-green-800">{success}</p>}
    {(!success || mode === "change") && <Button type="submit" isLoading={pending} disabled={mode === "change" && isLoading}>
      {mode === "forgot" ? "Gửi hướng dẫn đặt lại mật khẩu" : mode === "reset" ? "Đặt lại mật khẩu" : "Đổi mật khẩu"}
    </Button>}
    <Link href={mode === "change" ? "/profile" : "/login"} className="text-center text-sm text-brand-700 underline">
      {mode === "change" ? "Quay lại hồ sơ" : "Quay lại đăng nhập"}
    </Link>
  </form>;
}
