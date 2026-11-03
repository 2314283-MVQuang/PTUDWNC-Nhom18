"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { LayoutGrid, BookOpen, FolderTree, Users } from "lucide-react";

const NAV = [
  { href: "/dashboard", label: "Tổng quan", icon: LayoutGrid },
  { href: "/dashboard/recipes", label: "Công thức của tôi", icon: BookOpen },
  { href: "/dashboard/categories", label: "Danh mục", icon: FolderTree },
  { href: "/dashboard/users", label: "Người dùng", icon: Users, adminOnly: true },
];

export function DashboardSidebarNav({ isAdmin = false }: { isAdmin?: boolean }) {
  const pathname = usePathname();

  return (
    <aside className="flex flex-col gap-1 rounded-card-lg border border-brand-100 bg-white p-3 md:sticky md:top-24 md:self-start">
      <p className="px-3 py-2 text-xs font-bold uppercase tracking-wider text-neutral-400">Không gian quản lý</p>
      {NAV.filter(item => !item.adminOnly || isAdmin).map((item) => {
        const isActive =
          item.href === "/dashboard"
            ? pathname === "/dashboard"
            : pathname.startsWith(item.href);

        return (
          <Link
            key={item.href}
            href={item.href}
            className={`flex items-center gap-2.5 rounded-lg px-3.5 py-2.5 text-sm font-medium transition-all ${
              isActive
                ? "bg-brand-50 text-brand-700 font-semibold shadow-xs"
                : "text-neutral-600 hover:bg-neutral-100 hover:text-neutral-900"
            }`}
          >
            <item.icon className={`h-4 w-4 ${isActive ? "text-brand-600" : "text-neutral-400"}`} />
            {item.label}
          </Link>
        );
      })}
    </aside>
  );
}
