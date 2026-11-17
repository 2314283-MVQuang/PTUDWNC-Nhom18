import type { Metadata } from "next";
import { LegalPage } from "@/components/legal/LegalPage";

export const metadata: Metadata = {
  title: "Chính sách quyền riêng tư | Culinary Blog",
  description: "Culinary Blog thu thập, sử dụng và bảo vệ dữ liệu của bạn như thế nào.",
};

/**
 * Trang công khai bắt buộc khi xuất bản ứng dụng Google OAuth (Branding → Privacy policy link).
 * Nội dung mô tả đúng những gì hệ thống thực sự làm.
 */
export default function PrivacyPage() {
  return (
    <LegalPage
      title="Chính sách quyền riêng tư"
      description="Chúng tôi thu thập những dữ liệu gì, dùng vào việc gì và bạn có quyền gì với dữ liệu của mình."
      updated="17/11/2026"
      sections={[
        {
          heading: "1. Giới thiệu",
          paragraphs: [
            "Culinary Blog là website chia sẻ công thức nấu ăn, được xây dựng trong đồ án học phần Phát triển Ứng dụng Web Nâng cao (Nhóm 18, Trường Đại học Đà Lạt). Trang này giải thích cách website xử lý dữ liệu cá nhân của người dùng.",
          ],
        },
        {
          heading: "2. Dữ liệu chúng tôi thu thập",
          paragraphs: [
            "Khi đăng ký bằng email: địa chỉ email, tên hiển thị và mật khẩu. Mật khẩu chỉ được lưu dưới dạng mã băm (PBKDF2), không ai đọc được mật khẩu gốc.",
            "Khi đăng nhập bằng Google: chúng tôi chỉ yêu cầu các quyền cơ bản (openid, email, profile) để nhận địa chỉ email, họ tên và ảnh đại diện từ tài khoản Google. Chúng tôi không truy cập Gmail, Drive, danh bạ hay bất kỳ dữ liệu Google nào khác.",
            "Nội dung bạn tạo: công thức, ảnh tải lên, nguyên liệu, các bước thực hiện, thông tin dinh dưỡng và bình luận.",
            "Dữ liệu kỹ thuật: nhật ký truy cập (thời điểm, đường dẫn, địa chỉ IP) phục vụ bảo mật, chống spam và khắc phục lỗi.",
          ],
        },
        {
          heading: "3. Mục đích sử dụng",
          paragraphs: [
            "Tạo và quản lý tài khoản, cho phép bạn đăng nhập, đăng và quản lý công thức của mình.",
            "Hiển thị tên tác giả bên cạnh công thức và bình luận bạn đã công khai.",
            "Gửi email cần thiết cho tài khoản như email chào mừng, xác nhận email, đặt lại mật khẩu.",
            "Chúng tôi không bán, không cho thuê dữ liệu cá nhân và không dùng dữ liệu để quảng cáo.",
          ],
        },
        {
          heading: "4. Cookie",
          paragraphs: [
            "Website chỉ dùng cookie cần thiết để duy trì phiên đăng nhập (Auth.js) và bảo vệ chống giả mạo yêu cầu. Website không dùng cookie quảng cáo hay theo dõi của bên thứ ba.",
          ],
        },
        {
          heading: "5. Lưu trữ và bảo mật",
          paragraphs: [
            "Dữ liệu được lưu trên máy chủ của nhóm (cơ sở dữ liệu PostgreSQL và kho ảnh MinIO) và truyền qua kết nối HTTPS. Phiên đăng nhập dùng JWT thời hạn ngắn kèm refresh token được lưu dạng mã băm.",
            "Công thức bị xoá được giữ ở trạng thái xoá mềm 30 ngày để có thể khôi phục khi xoá nhầm, sau đó bị xoá vĩnh viễn cùng ảnh liên quan.",
          ],
        },
        {
          heading: "6. Quyền của bạn",
          paragraphs: [
            "Bạn có thể xem và sửa thông tin hồ sơ trong trang Hồ sơ, sửa hoặc xoá công thức và bình luận của mình bất kỳ lúc nào.",
            "Để yêu cầu xoá toàn bộ tài khoản và dữ liệu, hoặc thu hồi quyền đăng nhập bằng Google, bạn có thể liên hệ nhóm phát triển qua kho mã nguồn của dự án trên GitHub (2314283-MVQuang/PTUDWNC-Nhom18) hoặc gỡ quyền truy cập của ứng dụng tại myaccount.google.com/permissions.",
          ],
        },
        {
          heading: "7. Thay đổi chính sách",
          paragraphs: [
            "Khi chính sách thay đổi, ngày cập nhật ở đầu trang sẽ được sửa lại. Việc tiếp tục sử dụng website đồng nghĩa với việc bạn đồng ý với phiên bản mới.",
          ],
        },
      ]}
    />
  );
}
