import type { Metadata } from "next";
import { LegalPage } from "@/components/legal/LegalPage";

export const metadata: Metadata = {
  title: "Điều khoản sử dụng | Culinary Blog",
  description: "Quy định khi sử dụng Culinary Blog.",
};

/** Trang công khai dùng cho Google OAuth Branding → Terms of service link. */
export default function TermsPage() {
  return (
    <LegalPage
      title="Điều khoản sử dụng"
      description="Một vài quy định đơn giản để Culinary Blog là nơi chia sẻ công thức an toàn và thân thiện."
      updated="17/11/2026"
      sections={[
        {
          heading: "1. Phạm vi",
          paragraphs: [
            "Culinary Blog là sản phẩm đồ án học tập của Nhóm 18, Trường Đại học Đà Lạt. Website được cung cấp miễn phí, có thể tạm ngừng hoạt động hoặc thay đổi mà không báo trước.",
          ],
        },
        {
          heading: "2. Tài khoản",
          paragraphs: [
            "Bạn chịu trách nhiệm giữ an toàn mật khẩu và mọi hoạt động trên tài khoản của mình. Quản trị viên có thể khoá tài khoản vi phạm điều khoản.",
          ],
        },
        {
          heading: "3. Nội dung bạn đăng",
          paragraphs: [
            "Bạn giữ quyền đối với công thức và hình ảnh của mình, đồng thời cho phép website hiển thị chúng công khai khi bạn xuất bản.",
            "Không đăng nội dung vi phạm pháp luật, xúc phạm người khác, spam, hoặc hình ảnh bạn không có quyền sử dụng. Nội dung vi phạm có thể bị gỡ.",
          ],
        },
        {
          heading: "4. Miễn trừ trách nhiệm",
          paragraphs: [
            "Công thức và thông tin dinh dưỡng do người dùng tự cung cấp và chỉ mang tính tham khảo. Hãy cân nhắc dị ứng và tình trạng sức khoẻ của bạn trước khi nấu và ăn.",
          ],
        },
        {
          heading: "5. Quyền riêng tư",
          paragraphs: [
            "Cách chúng tôi xử lý dữ liệu cá nhân được mô tả trong trang Chính sách quyền riêng tư.",
          ],
        },
      ]}
    />
  );
}
