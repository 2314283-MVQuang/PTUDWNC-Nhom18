"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { LayoutGrid, BookOpen, FolderTree } from "lucide-react";

const NAV = [
  { href: "/dashboard", label: "Tổng quan", icon: LayoutGrid },
  { href: "/dashboard/recipes", label: "Công thức của tôi", icon: BookOpen },
  { href: "/dashboard/categories", label: "Danh mục", icon: FolderTree },
];

export function DashboardSidebarNav() {
  const pathname = usePathname();

  return (
    <aside className="flex flex-col gap-1">
      {NAV.map((item) => {
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
