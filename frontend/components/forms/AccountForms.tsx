"use client";

import Link from "next/link";
import { zodResolver } from "@hookform/resolvers/zod";
import { useSearchParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { apiErrorMessage } from "@/lib/api-client";
import { useChangePassword, useConfirmEmail, useForgotPassword, useResetPassword } from "@/hooks/useAccount";
import {
  changePasswordSchema, confirmEmailSchema, forgotPasswordSchema, resetPasswordSchema,
  type ChangePasswordFormValues, type ConfirmEmailFormValues, type ForgotPasswordFormValues,
  type ResetPasswordFormValues,
} from "@/lib/validations/auth.schema";

/**
 * Các form Auth nâng cao (quên/đặt lại/đổi mật khẩu, xác nhận email) — giao diện cho 4 endpoint
 * backend Tuần 3. Lỗi backend (token sai/hết hạn, mật khẩu hiện tại sai, quá giới hạn 429) được
 * hiển thị nguyên văn qua apiErrorMessage.
 */

const PASSWORD_HINT = "Tối thiểu 8 ký tự, có chữ hoa, số và ký tự đặc biệt.";

function Alert({ kind, children }: { kind: "error" | "success"; children: React.ReactNode }) {
  return kind === "error"
    ? <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{children}</p>
    : <p role="status" className="rounded-lg bg-green-50 px-3 py-2 text-sm text-green-700">{children}</p>;
}

/** /forgot-password — luôn báo "đã gửi" dù email có tồn tại hay không (backend chống dò email). */
export function ForgotPasswordForm() {
  const forgot = useForgotPassword();
  const [sentTo, setSentTo] = useState<string | null>(null);
  const { register, handleSubmit, formState: { errors } } = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
  });

  async function onSubmit(values: ForgotPasswordFormValues) {
    setSentTo(null);
    await forgot.mutateAsync(values.email);
    setSentTo(values.email);
  }

  return (
    <form onSubmit={handleSubmit(v => onSubmit(v).catch(() => undefined))} className="flex flex-col gap-4">
      {forgot.isError && <Alert kind="error">{apiErrorMessage(forgot.error, "Không gửi được yêu cầu, vui lòng thử lại.")}</Alert>}
      {sentTo && (
        <Alert kind="success">
          Nếu <strong>{sentTo}</strong> đã đăng ký, hướng dẫn đặt lại mật khẩu đã được gửi tới email này.
          Mở liên kết trong email hoặc <Link className="font-medium underline" href={`/reset-password?email=${encodeURIComponent(sentTo)}`}>nhập mã tại đây</Link>.
        </Alert>
      )}
      <Input label="Email đã đăng ký" type="email" autoComplete="email" error={errors.email?.message} {...register("email")} />
      <Button type="submit" isLoading={forgot.isPending}>Gửi hướng dẫn đặt lại mật khẩu</Button>
      <p className="text-center text-sm text-neutral-500">
        Nhớ ra mật khẩu? <Link href="/login" className="font-medium text-brand-700 hover:underline">Đăng nhập</Link>
      </p>
    </form>
  );
}

/** /reset-password?email=&token= — điền sẵn từ liên kết trong email, vẫn cho dán tay mã. */
export function ResetPasswordForm() {
  const searchParams = useSearchParams();
  const reset = useResetPassword();
  const [done, setDone] = useState(false);
  const { register, handleSubmit, formState: { errors } } = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { email: searchParams.get("email") ?? "", token: searchParams.get("token") ?? "" },
  });

  async function onSubmit(values: ResetPasswordFormValues) {
    setDone(false);
    await reset.mutateAsync({ email: values.email, token: values.token, newPassword: values.newPassword });
    setDone(true);
  }

  if (done) {
    return (
      <div className="flex flex-col gap-4">
        <Alert kind="success">Đặt lại mật khẩu thành công. Hãy đăng nhập bằng mật khẩu mới.</Alert>
        <Link href="/login"><Button className="w-full">Đến trang đăng nhập</Button></Link>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit(v => onSubmit(v).catch(() => undefined))} className="flex flex-col gap-4">
      {reset.isError && <Alert kind="error">{apiErrorMessage(reset.error, "Mã đặt lại không hợp lệ hoặc đã hết hạn.")}</Alert>}
      <Input label="Email" type="email" autoComplete="email" error={errors.email?.message} {...register("email")} />
      <Input label="Mã đặt lại mật khẩu (trong email)" autoComplete="one-time-code" error={errors.token?.message} {...register("token")} />
      <Input label="Mật khẩu mới" type="password" autoComplete="new-password" hint={PASSWORD_HINT} error={errors.newPassword?.message} {...register("newPassword")} />
      <Input label="Nhập lại mật khẩu mới" type="password" autoComplete="new-password" error={errors.confirmPassword?.message} {...register("confirmPassword")} />
      <Button type="submit" isLoading={reset.isPending}>Đặt lại mật khẩu</Button>
      <p className="text-center text-sm text-neutral-500">
        Chưa có mã? <Link href="/forgot-password" className="font-medium text-brand-700 hover:underline">Gửi lại email</Link>
      </p>
    </form>
  );
}

/** /confirm-email?userId=&token= — liên kết trong email đăng ký trỏ về đây. */
export function ConfirmEmailForm() {
  const searchParams = useSearchParams();
  const confirm = useConfirmEmail();
  const { register, handleSubmit, formState: { errors } } = useForm<ConfirmEmailFormValues>({
    resolver: zodResolver(confirmEmailSchema),
    defaultValues: { userId: searchParams.get("userId") ?? "", token: searchParams.get("token") ?? "" },
  });

  if (confirm.isSuccess) {
    return (
      <div className="flex flex-col gap-4">
        <Alert kind="success">Xác nhận email thành công. Cảm ơn bạn!</Alert>
        <Link href="/dashboard"><Button className="w-full">Vào trang quản lý</Button></Link>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit(v => confirm.mutateAsync(v).catch(() => undefined))} className="flex flex-col gap-4">
      {confirm.isError && <Alert kind="error">{apiErrorMessage(confirm.error, "Liên kết xác nhận không hợp lệ hoặc đã hết hạn.")}</Alert>}
      <Input label="Mã người dùng" error={errors.userId?.message} {...register("userId")} />
      <Input label="Mã xác nhận (trong email)" autoComplete="one-time-code" error={errors.token?.message} {...register("token")} />
      <Button type="submit" isLoading={confirm.isPending}>Xác nhận email</Button>
    </form>
  );
}

/** Đặt trong trang /profile — cần đăng nhập (UserId backend lấy từ JWT, không gửi từ form). */
export function ChangePasswordForm() {
  const change = useChangePassword();
  const [done, setDone] = useState(false);
  const { register, handleSubmit, reset, formState: { errors } } = useForm<ChangePasswordFormValues>({
    resolver: zodResolver(changePasswordSchema),
  });

  async function onSubmit(values: ChangePasswordFormValues) {
    setDone(false);
    await change.mutateAsync({ currentPassword: values.currentPassword, newPassword: values.newPassword });
    reset();
    setDone(true);
  }

  return (
    <form onSubmit={handleSubmit(v => onSubmit(v).catch(() => undefined))} className="flex flex-col gap-4">
      {change.isError && <Alert kind="error">{apiErrorMessage(change.error, "Đổi mật khẩu thất bại, vui lòng thử lại.")}</Alert>}
      {done && <Alert kind="success">Đã đổi mật khẩu. Lần đăng nhập sau hãy dùng mật khẩu mới.</Alert>}
      <Input label="Mật khẩu hiện tại" type="password" autoComplete="current-password" error={errors.currentPassword?.message} {...register("currentPassword")} />
      <Input label="Mật khẩu mới" type="password" autoComplete="new-password" hint={PASSWORD_HINT} error={errors.newPassword?.message} {...register("newPassword")} />
      <Input label="Nhập lại mật khẩu mới" type="password" autoComplete="new-password" error={errors.confirmPassword?.message} {...register("confirmPassword")} />
      <div className="flex justify-end">
        <Button type="submit" isLoading={change.isPending}>Đổi mật khẩu</Button>
      </div>
    </form>
  );
}
