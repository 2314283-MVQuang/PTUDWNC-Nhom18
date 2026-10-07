"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { signOut } from "next-auth/react";
import { useEffect, useState } from "react";
import { ChefHat, LayoutDashboard, LogOut, Menu, Search, User, X } from "lucide-react";
import { useCurrentUser } from "@/hooks/useAuth";
import { Button } from "@/components/ui/Button";
import { cn } from "@/lib/utils";

const NAV_LINKS = [
  { href: "/recipes", label: "Công thức" },
  { href: "/categories", label: "Danh mục" },
];

/**
 * Header hiển thị ở mọi trang (đặt trong app/layout.tsx).
 *
 * Điểm đáng chú ý về UI:
 *  - Dính trên đỉnh (sticky) và ĐỔI HÌNH DẠNG khi cuộn: ở đầu trang thì trong suốt để hoà
 *    vào hero màu cam, cuộn xuống thì chuyển sang nền kính mờ + đổ bóng cho dễ đọc.
 *  - Link menu có gạch chân trượt từ trái sang khi hover (class `.link-underline`),
 *    link của trang đang mở thì gạch chân luôn hiện (`data-active`).
 */
export function Header() {
  const { user, isAuthenticated, isLoading } = useCurrentUser();
  const router = useRouter();
  const pathname = usePathname();
  const [query, setQuery] = useState("");
  const [scrolled, setScrolled] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  // Trang chủ có hero màu cam đậm → header để trong suốt, chữ trắng cho đẹp.
  // Các trang khác nền sáng → header nền trắng ngay từ đầu.
  const overHero = pathname === "/" && !scrolled;

  useEffect(() => {
    function onScroll() {
      setScrolled(window.scrollY > 24);
    }
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  // Đổi trang thì đóng menu mobile lại, tránh menu còn mở đè lên nội dung mới.
  useEffect(() => {
    setMenuOpen(false);
  }, [pathname]);

  function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    const q = query.trim();
    if (q.length < 2) return;
    router.push(`/search?q=${encodeURIComponent(q)}`);
  }

  return (
    <header
      className={cn(
        "sticky top-0 z-50 transition-all duration-500 ease-springy",
        overHero
          ? "bg-transparent"
          : "border-b border-brand-100 bg-white/80 shadow-soft backdrop-blur-xl",
      )}
    >
      <div className="container-page flex h-16 items-center gap-4 sm:h-20 sm:gap-6">
        {/* Logo */}
        <Link href="/" className="group flex shrink-0 items-center gap-2.5">
          <span
            className={cn(
              "flex h-10 w-10 items-center justify-center rounded-xl shadow-soft transition-all duration-500 ease-springy group-hover:scale-110 group-hover:rotate-6",
              overHero ? "bg-white/20 backdrop-blur-md" : "bg-brand-gradient",
            )}
          >
            <ChefHat className="h-5 w-5 text-white transition-transform duration-500 group-hover:animate-wiggle" />
          </span>
          <span className="flex flex-col leading-none">
            <span
              className={cn(
                "font-display text-lg font-bold transition-colors duration-300",
                overHero ? "text-white" : "text-neutral-900",
              )}
            >
              Culinary
            </span>
            <span
              className={cn(
                "text-[11px] font-medium uppercase tracking-[0.18em] transition-colors duration-300",
                overHero ? "text-brand-100" : "text-brand-600",
              )}
            >
              Blog
            </span>
          </span>
        </Link>

        {/* Menu desktop */}
        <nav className="hidden items-center gap-6 md:flex">
          {NAV_LINKS.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              data-active={pathname.startsWith(link.href)}
              className={cn(
                "link-underline text-sm font-medium transition-colors duration-300",
                overHero ? "text-white/90 hover:text-white" : "text-neutral-600 hover:text-brand-700",
              )}
            >
              {link.label}
            </Link>
          ))}
        </nav>

        {/* Ô tìm kiếm — nở rộng ra khi focus */}
        <form onSubmit={handleSearch} className="ml-auto hidden max-w-xs flex-1 items-center lg:flex">
          <div className="group relative w-full">
            <Search
              className={cn(
                "pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 transition-colors duration-300",
                overHero ? "text-white/70" : "text-neutral-400",
              )}
            />
            <input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Tìm công thức..."
              aria-label="Tìm công thức"
              className={cn(
                "w-full rounded-full border py-2.5 pl-10 pr-4 text-sm transition-all duration-300 ease-springy focus:outline-none",
                overHero
                  ? "border-white/30 bg-white/15 text-white placeholder:text-white/60 backdrop-blur-md focus:border-white/60 focus:bg-white/25"
                  : "border-neutral-200 bg-white text-neutral-800 placeholder:text-neutral-400 focus:border-brand-400 focus:shadow-glow",
              )}
            />
          </div>
        </form>

        {/* Khu vực tài khoản */}
        <div className="ml-auto flex items-center gap-2 lg:ml-0">
          {isLoading ? (
            <span className="skeleton h-9 w-24 rounded-full" />
          ) : isAuthenticated ? (
            <>
              <Link
                href="/dashboard"
                className={cn(
                  "hidden items-center gap-1.5 rounded-full px-3 py-2 text-sm font-medium transition-colors duration-300 sm:inline-flex",
                  overHero ? "text-white/90 hover:bg-white/15" : "text-neutral-600 hover:bg-brand-50 hover:text-brand-700",
                )}
              >
                <LayoutDashboard className="h-4 w-4" />
                Quản lý
              </Link>
              <Link
                href="/profile"
                className="group flex items-center gap-2 rounded-full border border-transparent p-1 pr-3 transition-all duration-300 hover:border-brand-200 hover:bg-white"
              >
                <span className="flex h-8 w-8 items-center justify-center rounded-full bg-brand-gradient text-white shadow-soft transition-transform duration-300 ease-springy group-hover:scale-110">
                  <User className="h-4 w-4" />
                </span>
                <span
                  className={cn(
                    "hidden max-w-[9rem] truncate text-sm font-medium transition-colors sm:block",
                    overHero ? "text-white group-hover:text-brand-700" : "text-neutral-700",
                  )}
                >
                  {user?.name ?? "Hồ sơ"}
                </span>
              </Link>
              <Button
                variant="ghost"
                size="sm"
                aria-label="Đăng xuất"
                onClick={() => signOut({ callbackUrl: "/" })}
                className={cn("rounded-full", overHero && "text-white hover:bg-white/15")}
              >
                <LogOut className="h-4 w-4" />
              </Button>
            </>
          ) : (
            <>
              <Link href="/login" className="hidden sm:block">
                <Button
                  variant="ghost"
                  size="sm"
                  className={cn("rounded-full", overHero && "text-white hover:bg-white/15")}
                >
                  Đăng nhập
                </Button>
              </Link>
              <Link href="/register">
                <Button
                  size="sm"
                  className={cn(
                    "shine-on-hover rounded-full px-4 shadow-soft",
                    overHero && "bg-white text-brand-700 hover:bg-brand-50",
                  )}
                >
                  Đăng ký
                </Button>
              </Link>
            </>
          )}

          {/* Nút mở menu trên mobile */}
          <button
            type="button"
            onClick={() => setMenuOpen((open) => !open)}
            aria-label={menuOpen ? "Đóng menu" : "Mở menu"}
            aria-expanded={menuOpen}
            className={cn(
              "flex h-9 w-9 items-center justify-center rounded-full transition-colors duration-300 md:hidden",
              overHero ? "text-white hover:bg-white/15" : "text-neutral-600 hover:bg-brand-50",
            )}
          >
            {menuOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
          </button>
        </div>
      </div>

      {/* Menu thả xuống trên mobile — dùng grid-rows để trượt mở/đóng mượt mà */}
      <div
        className={cn(
          "grid overflow-hidden border-brand-100 bg-white/95 backdrop-blur-xl transition-all duration-500 ease-springy md:hidden",
          menuOpen ? "grid-rows-[1fr] border-t opacity-100" : "grid-rows-[0fr] opacity-0",
        )}
      >
        <div className="min-h-0">
          <nav className="container-page flex flex-col gap-1 py-4">
            {NAV_LINKS.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="rounded-lg px-3 py-2.5 text-sm font-medium text-neutral-700 transition-colors hover:bg-brand-50 hover:text-brand-700"
              >
                {link.label}
              </Link>
            ))}
            <Link
              href="/search"
              className="rounded-lg px-3 py-2.5 text-sm font-medium text-neutral-700 transition-colors hover:bg-brand-50 hover:text-brand-700"
            >
              Tìm kiếm
            </Link>
            {isAuthenticated ? (
              <Link
                href="/dashboard"
                className="rounded-lg px-3 py-2.5 text-sm font-medium text-neutral-700 transition-colors hover:bg-brand-50 hover:text-brand-700"
              >
                Trang quản lý
              </Link>
            ) : (
              <>
                <Link
                  href="/login"
                  className="rounded-lg px-3 py-2.5 text-sm font-medium text-neutral-700 transition-colors hover:bg-brand-50 hover:text-brand-700"
                >
                  Đăng nhập
                </Link>
                <Link
                  href="/register"
                  className="rounded-lg px-3 py-2.5 text-sm font-semibold text-brand-700 transition-colors hover:bg-brand-50"
                >
                  Đăng ký tài khoản
                </Link>
              </>
            )}
          </nav>
        </div>
      </div>
    </header>
  );
}
