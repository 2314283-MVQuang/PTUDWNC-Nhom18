import Link from "next/link";
import { LayoutGrid, BookOpen, FolderTree, Users } from "lucide-react";
import { auth } from "@/auth";

const NAV = [
  { href: "/dashboard", label: "Tổng quan", icon: LayoutGrid },
  { href: "/dashboard/recipes", label: "Công thức của tôi", icon: BookOpen },
  {
    href: "/dashboard/categories",
    label: "Danh mục",
    icon: FolderTree,
    adminOnly: true,
  },
  {
    href: "/dashboard/users",
    label: "Người dùng",
    icon: Users,
    adminOnly: true,
  },
];

/**
 * Layout cho toàn bộ /dashboard/** — sidebar điều hướng. Việc chặn truy cập khi chưa đăng nhập
 * đã xử lý ở middleware.ts (route matcher "/dashboard/:path*"), layout này không cần check lại.
 * Các mục Danh mục/Người dùng chỉ hiện cho Admin; middleware và API kiểm tra quyền độc lập.
 */
export default async function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await auth();
  return (
    <div className="container-page grid grid-cols-1 gap-6 py-8 md:grid-cols-[220px_minmax(0,1fr)]">
      <aside className="flex flex-col gap-1 rounded-card-lg border border-brand-100 bg-white p-3 md:sticky md:top-24 md:self-start">
        <p className="px-3 py-2 text-xs font-bold uppercase tracking-wider text-neutral-400">
          Không gian quản lý
        </p>
        {NAV.filter(
          (item) => !item.adminOnly || session?.roles?.includes("Admin"),
        ).map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-neutral-600 hover:bg-neutral-100 hover:text-neutral-900"
          >
            <item.icon className="h-4 w-4" />
            {item.label}
          </Link>
        ))}
      </aside>
      <div className="min-w-0">
        {session?.isDemo && (
          <p
            role="status"
            className="mb-4 rounded-lg bg-amber-50 p-3 text-sm text-amber-800"
          >
            Bạn đang dùng tài khoản demo. Đăng nhập bằng tài khoản chính thức để
            tạo, sửa hoặc xuất bản công thức.
          </p>
        )}
        {children}
      </div>
    </div>
  );
}
