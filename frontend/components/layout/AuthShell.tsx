import Link from "next/link";
import { ChefHat } from "lucide-react";
import { ChefIllustration } from "@/components/home/ChefIllustration";

interface AuthShellProps {
  title: string;
  subtitle: string;
  /** Dòng chữ + link ở cuối thẻ, vd. "Chưa có tài khoản? Đăng ký". */
  footer?: React.ReactNode;
  children: React.ReactNode;
}

const HIGHLIGHTS = [
  { emoji: "📖", text: "Lưu công thức yêu thích vào một nơi" },
  { emoji: "✍️", text: "Đăng món tủ của bạn chỉ trong 4 bước" },
  { emoji: "🔔", text: "Theo dõi công thức mới từ cộng đồng" },
];

/**
 * Khung 2 cột dùng chung cho trang đăng nhập và đăng ký.
 *
 * Cột trái là bảng màu cam có hình đầu bếp (ẩn trên màn hình nhỏ để dành chỗ cho form),
 * cột phải là thẻ trắng chứa form. Tách riêng thành component để 2 trang auth không phải
 * copy-paste cùng một đoạn layout.
 */
export function AuthShell({ title, subtitle, footer, children }: AuthShellProps) {
  return (
    <div className="grid min-h-[calc(100vh-4rem)] lg:grid-cols-2">
      {/* ----- Cột trái: giới thiệu ----- */}
      <aside className="relative hidden overflow-hidden bg-brand-gradient lg:flex lg:flex-col lg:justify-center">
        <div
          aria-hidden
          className="absolute inset-0 opacity-[0.14]"
          style={{
            backgroundImage: "radial-gradient(#ffffff 1.5px, transparent 1.5px)",
            backgroundSize: "24px 24px",
          }}
        />
        <div aria-hidden className="absolute -left-20 top-10 h-72 w-72 rounded-full bg-amber-300/30 blur-3xl" />

        <div className="relative px-12 py-16">
          <Link href="/" className="group inline-flex items-center gap-2.5">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/20 backdrop-blur-md transition-transform duration-500 ease-springy group-hover:rotate-6 group-hover:scale-110">
              <ChefHat className="h-5 w-5 text-white" />
            </span>
            <span className="font-display text-xl font-bold text-white">Culinary Blog</span>
          </Link>

          <div className="mx-auto mt-6 w-full max-w-sm">
            <ChefIllustration />
          </div>

          <ul className="mt-6 space-y-3">
            {HIGHLIGHTS.map((item, index) => (
              <li
                key={item.text}
                className="flex animate-fade-up items-center gap-3 text-sm text-white/90"
                style={{ animationDelay: `${index * 120}ms` }}
              >
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white/15 text-lg backdrop-blur-md">
                  {item.emoji}
                </span>
                {item.text}
              </li>
            ))}
          </ul>
        </div>
      </aside>

      {/* ----- Cột phải: form ----- */}
      <main className="flex items-center justify-center px-4 py-12 sm:px-8">
        <div className="w-full max-w-md animate-fade-up">
          <div className="rounded-card-lg border border-brand-100 bg-white p-6 sm:p-8 shadow-lift">
            <h1 className="text-center font-display text-2xl font-bold text-neutral-900">{title}</h1>
            <p className="mt-2 text-center text-sm text-neutral-500">{subtitle}</p>
            <div className="mt-6">{children}</div>
          </div>
          {footer && <p className="mt-5 text-center text-sm text-neutral-500">{footer}</p>}
        </div>
      </main>
    </div>
  );
}
