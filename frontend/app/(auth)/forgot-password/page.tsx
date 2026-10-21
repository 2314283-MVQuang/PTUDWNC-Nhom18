import { AuthShell } from "@/components/layout/AuthShell";
import { ForgotPasswordForm } from "@/components/forms/AccountForms";

export const metadata = { title: "Quên mật khẩu" };

export default function ForgotPasswordPage() {
  return (
    <AuthShell title="Quên mật khẩu" subtitle="Nhập email đã đăng ký, chúng tôi sẽ gửi hướng dẫn đặt lại mật khẩu">
      <ForgotPasswordForm />
    </AuthShell>
  );
}
