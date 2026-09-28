import Link from "next/link";
import { ChefHat, Github, Mail } from "lucide-react";

const LINK_GROUPS = [
  {
    title: "Khám phá",
    links: [
      { href: "/recipes", label: "Tất cả công thức" },
      { href: "/categories", label: "Danh mục món" },
      { href: "/search", label: "Tìm kiếm" },
    ],
  },
  {
    title: "Tài khoản",
    links: [
      { href: "/login", label: "Đăng nhập" },
      { href: "/register", label: "Đăng ký" },
      { href: "/dashboard/recipes/new", label: "Đăng công thức" },
    ],
  },
];

/** Footer hiển thị ở mọi trang — nền tối ấm, chia cột, có dải emoji món ăn chạy ngang. */
export function Footer() {
  return (
    <footer className="relative mt-20 overflow-hidden bg-neutral-900 text-neutral-300">
      {/* Viền gradient mảnh ở mép trên */}
      <div className="h-1 w-full bg-brand-gradient" />

      {/* Dải emoji món ăn chạy ngang vô tận — nhân đôi mảng để vòng lặp không bị hụt */}
      <div className="border-b border-white/5 py-3">
        <div className="flex w-max animate-marquee gap-8 whitespace-nowrap text-xl opacity-40">
          {Array.from({ length: 2 }).map((_, copy) => (
            <span key={copy} className="flex gap-8" aria-hidden>
              {"🍜 🍕 🥗 🍰 🍳 🥘 🍤 🌮 🍲 🥐 🍣 🥟".split(" ").map((emoji, i) => (
                <span key={`${copy}-${i}`}>{emoji}</span>
              ))}
            </span>
          ))}
        </div>
      </div>

      <div className="container-page grid gap-10 py-12 sm:grid-cols-2 lg:grid-cols-4">
        <div className="lg:col-span-2">
          <Link href="/" className="group inline-flex items-center gap-2.5">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-gradient shadow-soft transition-transform duration-500 ease-springy group-hover:rotate-6 group-hover:scale-110">
              <ChefHat className="h-5 w-5 text-white" />
            </span>
            <span className="font-display text-xl font-bold text-white">Culinary Blog</span>
          </Link>
          <p className="mt-4 max-w-sm text-sm leading-relaxed text-neutral-400">
            Nền tảng chia sẻ, khám phá và lưu trữ công thức nấu ăn. Dự án thực hành xây dựng bằng
            Next.js và .NET.
          </p>
          <div className="mt-5 flex gap-3">
            <a
              href="mailto:hello@culinaryblog.dev"
              aria-label="Gửi email"
              className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/10 text-neutral-400 transition-all duration-300 ease-springy hover:-translate-y-1 hover:border-brand-400 hover:bg-brand-500 hover:text-white"
            >
              <Mail className="h-4 w-4" />
            </a>
            <a
              href="https://github.com"
              target="_blank"
              rel="noreferrer"
              aria-label="GitHub"
              className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/10 text-neutral-400 transition-all duration-300 ease-springy hover:-translate-y-1 hover:border-brand-400 hover:bg-brand-500 hover:text-white"
            >
              <Github className="h-4 w-4" />
            </a>
          </div>
        </div>

        {LINK_GROUPS.map((group) => (
          <div key={group.title}>
            <h3 className="font-display text-sm font-semibold uppercase tracking-[0.14em] text-white">
              {group.title}
            </h3>
            <ul className="mt-4 space-y-2.5">
              {group.links.map((link) => (
                <li key={link.href}>
                  <Link
                    href={link.href}
                    className="group inline-flex items-center gap-2 text-sm text-neutral-400 transition-colors duration-300 hover:text-brand-400"
                  >
                    <span className="h-px w-0 bg-brand-400 transition-all duration-300 ease-springy group-hover:w-4" />
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

      <div className="border-t border-white/5">
        <div className="container-page flex flex-col items-center justify-between gap-2 py-6 text-xs text-neutral-500 sm:flex-row">
          <p>© {new Date().getFullYear()} Culinary Blog. Chia sẻ công thức nấu ăn.</p>
          <p>Xây dựng với Next.js 15 + .NET 10</p>
        </div>
      </div>
    </footer>
  );
}
