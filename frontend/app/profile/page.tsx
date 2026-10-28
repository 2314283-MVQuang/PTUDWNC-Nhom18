"use client";

import { ProfileForm } from "@/components/forms/ProfileForm";
import { ChangePasswordForm } from "@/components/forms/AccountForms";
import { ErrorBlock, LoadingBlock } from "@/components/ui/Spinner";
import { useMyProfile } from "@/hooks/useProfile";
import { useCurrentUser } from "@/hooks/useAuth";
import Link from "next/link";

// Mục 9: "/profile" — CSR, yêu cầu đăng nhập (middleware.ts đã chặn ở matcher "/profile/:path*").
export default function ProfilePage() {
  const { data: profile, isLoading, isError, refetch } = useMyProfile();
  const { isDemo } = useCurrentUser();

  return (
    <div className="container-page flex justify-center py-10">
      <div className="w-full max-w-lg">
        <h1 className="mb-6 text-2xl font-bold text-neutral-900">Hồ sơ cá nhân</h1>

        {isDemo && <p role="status">Tài khoản demo chỉ dùng xem giao diện. Hãy đăng nhập bằng tài khoản hệ thống để cập nhật hồ sơ.</p>}
        {isLoading && <LoadingBlock />}
        {isError && <ErrorBlock message="Không tải được hồ sơ." onRetry={() => refetch()} />}
        {profile && <ProfileForm key={profile.id} user={profile} />}
        {!isDemo && <Link href="/profile/change-password" className="mt-6 inline-block text-brand-700 underline">Đổi mật khẩu</Link>}
      </div>
    </div>
  );
}
