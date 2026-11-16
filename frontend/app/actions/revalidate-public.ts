"use server";

import { revalidatePath } from "next/cache";
import { auth } from "@/auth";

/**
 * Làm mới cache ISR của các trang công khai ngay sau khi người dùng sửa dữ liệu.
 *
 * Trang chủ (1 giờ), chi tiết công thức (5 phút), danh mục (10 phút–1 giờ) được Next.js
 * render sẵn rồi giữ trong cache; thiếu bước này thì sửa công thức, thêm dinh dưỡng, xuất bản...
 * xong vẫn thấy bản cũ cho tới khi hết hạn revalidate. API phía sau đã tự xoá Output Cache theo
 * tag nên chỉ cần xoá cache phía Next.js.
 *
 * Chỉ người đã đăng nhập (người vừa ghi dữ liệu) mới gọi được, tránh bị spam làm mất cache.
 */
export async function revalidatePublicPages(): Promise<void> {
  const session = await auth();
  if (!session?.user) return;
  revalidatePath("/", "layout");
}
