import type { NextConfig } from "next";

/**
 * Cấu hình Next.js cho Culinary Blog.
 *
 * - images.remotePatterns: cho phép Next/Image load ảnh từ MinIO (S3-compatible),
 *   theo mục 6.1 (Object Storage: MinIO) và FR-FILE-001 trong tài liệu đặc tả.
 *   Interns: khi deploy thật, thay "localhost" bằng domain MinIO thật (vd. cdn.culinaryblog.com).
 * - Không dùng `output: "export"` vì ta cần SSR/ISR (mục 9 - Sitemap Frontend).
 * - `output: "standalone"`: đóng gói kèm node_modules cần thiết vào `.next/standalone`,
 *   giúp Dockerfile (mục 11) build image nhỏ gọn thay vì copy nguyên node_modules.
 */
const nextConfig: NextConfig = {
  output: "standalone",
  images: {
    remotePatterns: [
      {
        protocol: "http",
        hostname: "localhost",
        port: "9000",
        pathname: "/culinary-blog/**",
      },
      {
        // Backend .NET khi còn dùng LocalFileStorageService: ảnh nằm trong wwwroot/uploads của
        // chính API ở cổng 5000. Bỏ mục này đi khi đã chuyển hẳn sang MinIO (FR-FILE-001).
        protocol: "http",
        hostname: "localhost",
        port: "5000",
        pathname: "/uploads/**",
      },
      {
        protocol: "https",
        hostname: "**.amazonaws.com",
      },
    ],
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
