import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

/**
 * Gộp class Tailwind an toàn, tự loại bỏ xung đột (vd. "p-2 p-4" -> chỉ giữ "p-4").
 * Dùng ở MỌI component nhận prop `className` thay vì nối chuỗi bằng tay.
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/** Format phút thành dạng "1h 30p" / "45p" cho dễ đọc — dùng ở RecipeCard, RecipeDetail. */
export function formatMinutes(minutes: number): string {
  if (minutes <= 0) return "0p";
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) return `${rest}p`;
  if (rest === 0) return `${hours}h`;
  return `${hours}h ${rest}p`;
}

/** Format ngày ISO -> "dd/MM/yyyy" theo locale Việt Nam. */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return "";
  return new Date(iso).toLocaleDateString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

/** Map enum RecipeDifficulty sang nhãn tiếng Việt hiển thị trên UI. */
export const DIFFICULTY_LABEL: Record<string, string> = {
  Easy: "Dễ",
  Medium: "Trung bình",
  Hard: "Khó",
  Expert: "Chuyên gia",
};

/** Map enum RecipeStatus sang nhãn + màu badge tương ứng, dùng trong dashboard. */
export const STATUS_LABEL: Record<string, { label: string; className: string }> = {
  Draft: { label: "Bản nháp", className: "border-neutral-200 bg-neutral-100 text-neutral-600" },
  Published: { label: "Đã đăng", className: "border-herb-200 bg-herb-50 text-herb-700" },
  Archived: { label: "Đã lưu trữ", className: "border-amber-200 bg-amber-50 text-amber-700" },
};

/**
 * Backend trả URL ảnh dạng TƯƠNG ĐỐI ("/uploads/recipes/.../abc.png" — xem LocalFileStorageService).
 * Frontend chạy ở cổng 3000 còn file nằm ở cổng 5000, nên dán thẳng vào <Image src> sẽ ra 404.
 * Hàm này ghép thêm origin của backend; URL tuyệt đối (sau này khi chuyển sang MinIO) giữ nguyên.
 */
const API_ORIGIN = (process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000/api/v1").replace(
  /\/api\/v\d+\/?$/,
  "",
);

export function resolveMediaUrl(url: string | null | undefined): string | null {
  if (!url) return null;
  if (/^https?:\/\//i.test(url)) return url;
  return `${API_ORIGIN}${url.startsWith("/") ? "" : "/"}${url}`;
}

/**
 * Làm tròn số đến 1 chữ số thập phân (ví dụ: 1.4, 494.6).
 * Bỏ .0 nếu là số nguyên (ví dụ: 2 thay vì 2.0).
 */
export function formatDecimal(val: number | string | null | undefined): string {
  if (val === null || val === undefined || val === "") return "";
  const num = typeof val === "number" ? val : parseFloat(val);
  if (isNaN(num)) return "";
  return (Math.round(num * 10) / 10).toString();
}
