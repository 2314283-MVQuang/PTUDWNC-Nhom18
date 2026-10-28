import { PasswordForm } from "@/components/forms/PasswordForm";

export const metadata = { title: "Đổi mật khẩu", robots: { index: false } };

export default function ChangePasswordPage() {
  return <div className="container-page flex justify-center py-10">
    <section className="w-full max-w-lg space-y-6">
      <h1 className="text-2xl font-bold">Đổi mật khẩu</h1>
      <PasswordForm mode="change" />
    </section>
  </div>;
}
