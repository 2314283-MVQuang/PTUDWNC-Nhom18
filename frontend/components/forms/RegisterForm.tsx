"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { signIn } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { apiFetch, ApiError } from "@/lib/api-client";
import { registerSchema, type RegisterFormValues } from "@/lib/validations/auth.schema";
import { ApiErrorCode } from "@/types/common";

/** Form đăng ký tài khoản mới (FR-AUTH-001) — sau khi đăng ký thành công, tự đăng nhập luôn. */
export function RegisterForm() {
  const router = useRouter();
  const [formError, setFormError] = useState<string | null>(null);

  const [formSuccess, setFormSuccess] = useState<string | null>(null);

  const {
    register: registerField,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({ resolver: zodResolver(registerSchema) });

  async function onSubmit(values: RegisterFormValues) {
    setFormError(null);
    setFormSuccess(null);
    try {
      // Gọi API đăng ký (lưu vào bộ nhớ và đồng bộ)
      const res = await fetch("/api/auth/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });

      const data = await res.json();

      if (!res.ok) {
        setFormError(data.error || "Đăng ký thất bại, vui lòng thử lại.");
        return;
      }

      setFormSuccess("Đăng ký tài khoản thành công! Đang tự động đăng nhập...");

      // Đăng ký xong thì tự đăng nhập bằng chính email/password vừa nhập
      const loginRes = await signIn("credentials", {
        email: values.email,
        password: values.password,
        redirect: false,
      });

      if (loginRes?.error) {
        // Nếu không tự đăng nhập được thì chuyển sang trang login với email đã điền
        router.push(`/login?email=${encodeURIComponent(values.email)}`);
        return;
      }

      router.push("/dashboard");
      router.refresh();
    } catch {
      setFormError("Không thể kết nối máy chủ. Vui lòng thử lại sau.");
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
      {formError && (
        <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
          {formError}
        </p>
      )}

      {formSuccess && (
        <p role="status" className="rounded-lg bg-green-50 px-3 py-2 text-sm text-green-700">
          {formSuccess}
        </p>
      )}

      <Input label="Họ và tên" error={errors.fullName?.message} {...registerField("fullName")} />
      <Input label="Email" type="email" autoComplete="email" error={errors.email?.message} {...registerField("email")} />
      <Input label="Tên đăng nhập" error={errors.userName?.message} {...registerField("userName")} />
      <Input
        label="Mật khẩu"
        type="password"
        autoComplete="new-password"
        hint="Tối thiểu 8 ký tự, có chữ hoa, số và ký tự đặc biệt."
        error={errors.password?.message}
        {...registerField("password")}
      />
      <Input
        label="Xác nhận mật khẩu (Nhập lại lần 2)"
        type="password"
        autoComplete="new-password"
        hint="Nhập lại chính xác mật khẩu đã đặt ở trên."
        error={errors.confirmPassword?.message}
        {...registerField("confirmPassword")}
      />

      <Button type="submit" isLoading={isSubmitting} className="mt-2">
        Đăng ký tài khoản
      </Button>
      {/* Link "Đã có tài khoản?" nằm ở AuthShell (dưới thẻ form) nên không lặp lại ở đây. */}
    </form>
  );
}
