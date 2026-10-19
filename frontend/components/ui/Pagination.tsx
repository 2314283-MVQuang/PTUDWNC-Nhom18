import Link from "next/link";
import { cn } from "@/lib/utils";
import type { PageMeta } from "@/types/common";

/**
 * Phân trang dạng link (offset-based, khớp mục 8: page/pageSize/totalPages).
 * `buildHref` để mỗi trang tự quyết định URL (giữ lại query filter hiện có).
 */
export function Pagination({ meta, buildHref }: { meta: PageMeta; buildHref: (page: number) => string }) {
  if (meta.totalPages <= 1) return null;

  const pages = Array.from({ length: meta.totalPages }, (_, i) => i + 1);

  return (
    <nav className="mt-8 flex items-center justify-center gap-1" aria-label="Phân trang">
      {pages.map((page) => (
        <Link
          key={page}
          href={buildHref(page)}
          aria-label={`Trang ${page}`}
          aria-current={page === meta.page ? "page" : undefined}
          className={cn(
            "flex h-9 w-9 items-center justify-center rounded-lg text-sm font-medium",
            page === meta.page ? "bg-brand-600 text-white" : "text-neutral-600 hover:bg-neutral-100",
          )}
        >
          {page}
        </Link>
      ))}
    </nav>
  );
}
