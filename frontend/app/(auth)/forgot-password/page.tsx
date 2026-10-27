import { AuthShell } from "@/components/layout/AuthShell";
import { PasswordForm } from "@/components/forms/PasswordForm";

export const metadata = { title: "Quên mật khẩu", robots: { index: false } };

export default function ForgotPasswordPage() {
  return <AuthShell title="Quên mật khẩu" subtitle="Nhập email đã đăng ký để nhận hướng dẫn đặt lại mật khẩu.">
    <PasswordForm mode="forgot" />
  </AuthShell>;
}
