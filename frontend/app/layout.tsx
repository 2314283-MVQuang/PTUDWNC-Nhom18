import type { Metadata } from "next";
import { Inter, Playfair_Display } from "next/font/google";
import { Header } from "@/components/layout/Header";
import { Footer } from "@/components/layout/Footer";
import { CursorGlow } from "@/components/ui/CursorGlow";
import { ScrollProgress } from "@/components/ui/ScrollProgress";
import { Providers } from "./providers";
import "./globals.css";

/**
 * Font được nạp bằng `next/font/google`: Next.js tải font về lúc BUILD rồi tự host cùng app,
 * nên trang không phải gọi sang server Google lúc chạy (nhanh hơn + không bị nhảy chữ).
 * Giá trị được gắn vào biến CSS --font-sans / --font-display mà tailwind.config.ts đang dùng.
 */
const inter = Inter({
  subsets: ["latin", "vietnamese"],
  variable: "--font-sans",
  display: "swap",
});

const playfair = Playfair_Display({
  subsets: ["latin", "vietnamese"],
  variable: "--font-display",
  display: "swap",
  weight: ["600", "700", "800"],
});

export const metadata: Metadata = {
  title: {
    default: "Culinary Blog — Chia sẻ công thức nấu ăn",
    template: "%s | Culinary Blog",
  },
  description: "Nền tảng chia sẻ, khám phá và lưu trữ công thức nấu ăn.",
};

/**
 * Root layout — bọc mọi trang trong app. Header/Footer đặt ở đây để không phải lặp lại
 * ở từng page.tsx (mục 6.1: Next.js App Router).
 *
 * `ScrollProgress` và `CursorGlow` là 2 hiệu ứng toàn cục: thanh tiến trình cuộn ở đỉnh
 * trang và vầng sáng bám theo con trỏ. Cả hai đều tự tắt khi người dùng bật chế độ
 * "giảm chuyển động" hoặc dùng thiết bị cảm ứng.
 */
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi" className={`${inter.variable} ${playfair.variable}`}>
      <body className="flex min-h-screen flex-col font-sans antialiased">
        <a href="#main-content" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[100] focus:rounded-lg focus:bg-white focus:px-4 focus:py-3 focus:text-brand-800 focus:shadow-lg">Đến nội dung chính</a>
        <Providers>
          <ScrollProgress />
          <CursorGlow />
          <Header />
          <main id="main-content" tabIndex={-1} className="flex-1">{children}</main>
          <Footer />
        </Providers>
      </body>
    </html>
  );
}
