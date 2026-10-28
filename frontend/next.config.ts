import type { NextConfig } from "next";

/**
 * Cấu hình Next.js cho Culinary Blog.
 *
 * - images.unoptimized: ảnh tải thẳng từ MinIO (FR-FILE-001), xem chú thích bên dưới.
 * - Không dùng `output: "export"` vì ta cần SSR/ISR (mục 9 - Sitemap Frontend).
 * - `output: "standalone"`: đóng gói kèm node_modules cần thiết vào `.next/standalone`,
 *   giúp Dockerfile (mục 11) build image nhỏ gọn thay vì copy nguyên node_modules.
 */
const nextConfig: NextConfig = {
  output: "standalone",
  // Giới hạn worker build để máy phát triển vẫn đủ bộ nhớ khi chạy Docker/.NET cùng lúc.
  experimental: { cpus: 1 },
  images: {
    // Tuần 8 (triển khai): ảnh công thức nằm trên MinIO với domain đổi theo nơi deploy
    // (localhost:9000 khi dev, domain thật khi chạy production) và đã được job FR-JOB-002 resize
    // sẵn bản medium/thumbnail — không cần bộ tối ưu ảnh của Next.js. Tắt tối ưu để <Image> nhận
    // mọi URL ảnh hợp lệ mà không phải khai báo trước từng hostname (trước đây khai sai bucket
    // "/culinary-blog/**" trong khi bucket thật là "recipe-images" nên ảnh danh mục/bước bị lỗi).
    unoptimized: true,
  },
  // Bật React strict mode để bắt lỗi sớm trong quá trình dev - tốt cho intern học.
  reactStrictMode: true,
  async rewrites() {
    return [
      {
        source: "/auth/login",
        destination: "/login",
      },
      {
        source: "/auth/register",
        destination: "/register",
      },
    ];
  },
};

export default nextConfig;
