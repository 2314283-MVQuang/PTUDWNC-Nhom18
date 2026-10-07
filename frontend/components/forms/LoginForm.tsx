"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { signIn } from "next-auth/react";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { loginSchema, type LoginFormValues } from "@/lib/validations/auth.schema";

/** Form đăng nhập email/password + nút Google (FR-AUTH-002, FR-AUTH-003). */
export function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const callbackUrl = searchParams.get("callbackUrl") ?? "/dashboard";
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({ resolver: zodResolver(loginSchema) });

  function handleQuickFill(email: string, pass: string) {
    setValue("email", email);
    setValue("password", pass);
    setFormError(null);
  }

  async function onSubmit(values: LoginFormValues) {
    setFormError(null);
    // FR-AUTH-002: sai email/password KHÔNG được tiết lộ email có tồn tại hay không (chống
    // User Enumeration Attack) — nên chỉ hiển thị 1 thông báo chung chung ở đây.
    const result = await signIn("credentials", { ...values, redirect: false });

    if (result?.error) {
      setFormError("Email hoặc mật khẩu không đúng.");
      return;
    }
    router.push(callbackUrl);
    router.refresh();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
      {/* Hộp hiển thị tài khoản mẫu trong đề để test */}
      <div className="rounded-xl border border-brand-200 bg-brand-50/80 p-3 text-xs text-neutral-700 shadow-sm">
        <p className="font-semibold text-brand-900 mb-2 flex items-center gap-1.5 text-xs">
          <span>🔑</span> Tài khoản mẫu trong đề bài (Bấm để điền nhanh):
        </p>
        <div className="flex flex-col gap-2">
          <button
            type="button"
            onClick={() => handleQuickFill("admin@culinaryblog.local", "Admin@123")}
            className="flex items-center justify-between rounded-lg border border-brand-200 bg-white p-2 text-left hover:border-brand-400 hover:bg-brand-50 transition-colors shadow-xs cursor-pointer"
          >
            <div>
              <span className="font-semibold text-neutral-800">Admin:</span> admin@culinaryblog.local
              <div className="text-[11px] text-neutral-500 font-mono">Mật khẩu: Admin@123 (Role: Admin)</div>
            </div>
            <span className="text-[11px] font-medium text-brand-700 bg-brand-100 px-2.5 py-1 rounded-md">
              Điền ngay
            </span>
          </button>

          <button
            type="button"
            onClick={() => handleQuickFill("author@culinaryblog.local", "Author@123")}
            className="flex items-center justify-between rounded-lg border border-brand-200 bg-white p-2 text-left hover:border-brand-400 hover:bg-brand-50 transition-colors shadow-xs cursor-pointer"
          >
            <div>
              <span className="font-semibold text-neutral-800">Author:</span> author@culinaryblog.local
              <div className="text-[11px] text-neutral-500 font-mono">Mật khẩu: Author@123 (Role: Author)</div>
            </div>
            <span className="text-[11px] font-medium text-brand-700 bg-brand-100 px-2.5 py-1 rounded-md">
              Điền ngay
            </span>
          </button>
        </div>
      </div>

      {formError && (
        <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
          {formError}
        </p>
      )}

      <Input label="Email" type="email" autoComplete="email" error={errors.email?.message} {...register("email")} />
      <Input
        label="Mật khẩu"
        type="password"
        autoComplete="current-password"
        error={errors.password?.message}
        {...register("password")}
      />

      <Button type="submit" isLoading={isSubmitting} className="mt-2">
        Đăng nhập
      </Button>

      <div className="relative py-2 text-center text-xs text-neutral-400">
        <span className="relative z-10 bg-white px-2">hoặc</span>
        <div className="absolute inset-x-0 top-1/2 -z-0 border-t border-neutral-200" />
      </div>

      <Button type="button" variant="outline" onClick={() => signIn("google", { callbackUrl })}>
        Đăng nhập với Google
      </Button>
      {/* Link "Chưa có tài khoản?" nằm ở AuthShell (dưới thẻ form) nên không lặp lại ở đây. */}
    </form>
  );
}
