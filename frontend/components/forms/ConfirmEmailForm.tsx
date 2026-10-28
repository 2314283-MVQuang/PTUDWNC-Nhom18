"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useState } from "react";
import { apiErrorMessage, apiFetch } from "@/lib/api-client";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";

/**
 * /confirm-email?userId=&token= — gọi POST /auth/confirm-email (Auth nâng cao Tuần 3).
 * Token Identity là base64 nên có thể chứa "+"; dán từ link không mã hoá thì "+" thành dấu cách,
 * nên đổi lại trước khi gửi.
 */
export function ConfirmEmailForm() {
  const searchParams = useSearchParams();
  const [userId, setUserId] = useState(searchParams.get("userId") ?? "");
  const [token, setToken] = useState(searchParams.get("token") ?? "");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setError("");
    if (!userId.trim() || !token.trim()) {
      setError("Vui lòng nhập mã người dùng và mã xác nhận trong email.");
      return;
    }
    setPending(true);
    try {
      await apiFetch<void>("/auth/confirm-email", {
        method: "POST",
        body: { userId: userId.trim(), token: token.trim().replace(/ /g, "+") },
      });
      setDone(true);
    } catch (err) {
      setError(apiErrorMessage(err, "Liên kết xác nhận không hợp lệ hoặc đã hết hạn."));
    } finally {
      setPending(false);
    }
  }

  if (done) {
    return (
      <div className="flex flex-col gap-4">
        <p role="status" className="rounded-lg bg-green-50 px-3 py-2 text-sm text-green-700">Xác nhận email thành công. Cảm ơn bạn!</p>
        <Link href="/dashboard"><Button className="w-full">Vào trang quản lý</Button></Link>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-4">
      {error && <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}
      <Input label="Mã người dùng" name="userId" value={userId} onChange={e => setUserId(e.target.value)} />
      <Input label="Mã xác nhận (trong email)" name="token" autoComplete="one-time-code" value={token} onChange={e => setToken(e.target.value)} />
      <Button type="submit" isLoading={pending}>Xác nhận email</Button>
    </form>
  );
}
