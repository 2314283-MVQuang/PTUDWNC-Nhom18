# Culinary Blog — Frontend

Next.js App Router + TypeScript, theo mục 6.1 và mục 9 (Sitemap Frontend) của tài liệu
đặc tả `huong_dan_xay_dung_culinary_blog.md`.

## Công nghệ dùng

Phần việc tuần 1–6, các yêu cầu tối thiểu, cấu hình bảo mật và kết quả kiểm chứng được ghi tại
[báo cáo tổng hợp Chung Thiện Ý tuần 1–6](../spec/bao_cao_tong_hop_ChungThienY_tuan_1_6.md).

Tuần 6 bổ sung FR-SRCH-002 trên `/search`: bộ lọc kết hợp, Context/reducer store,
Radix Dialog cho mobile và component loading/error/empty. `npm test` chạy cả Auth và Search.
Hướng dẫn test PostgreSQL/Redis tạm và trình duyệt nằm ở mục 13.8–13.9 của báo cáo.
Search vẫn trả mảng tối đa 50 kết quả; sorting/pagination API thuộc phần phân công khác.

| Thư viện | Vai trò |
|---|---|
| Next.js 15 (App Router) | Framework React, SSR/ISR cho SEO |
| TypeScript | Kiểu tĩnh, khớp DTO với backend |
| Tailwind CSS | Styling utility-first |
| @radix-ui/react-dialog 1.3.1 | Dialog bộ lọc mobile, focus trap và Escape |
| Auth.js v5 (`next-auth`) | Đăng nhập email/password + Google OAuth |
| TanStack Query | Cache & đồng bộ dữ liệu server (recipes, categories...) |
| React Hook Form + Zod | Quản lý form + validate |

## Bắt đầu — chỉ 3 lệnh

Cần **Node.js 20 trở lên** (khuyến nghị bản LTS mới nhất).

```bash
git clone https://github.com/2314283-MVQuang/CulinaryBlog.git
cd CulinaryBlog/frontend
npm ci
npm run dev
```

Mở http://localhost:3000 là xong.

Không cần tự tạo file `.env` gì cả: lệnh `npm run dev` sẽ tự chạy `scripts/setup-env.mjs`
để tạo `.env.local` từ `.env.local.example` kèm một `AUTH_SECRET` ngẫu nhiên. Nếu bạn đã có
`.env.local` rồi thì script bỏ qua, không ghi đè.

**Backend chưa chạy cũng không sao.** Các trang đã bọc try-catch nên vẫn hiển thị giao diện
bình thường, chỉ kèm một dòng thông báo màu vàng "chưa kết nối được tới backend". Khi nào API
.NET chạy thì dữ liệu tự lên, không phải sửa code.

### Các biến trong `.env.local`

| Biến | Ý nghĩa |
|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | URL backend .NET, mặc định `http://localhost:5000/api/v1`. Backend chạy cổng khác thì sửa dòng này |
| `API_INTERNAL_BASE_URL` | URL backend để Next.js server gọi nội bộ, ví dụ `http://api:8080/api/v1`; để trống sẽ dùng URL public |
| `AUTH_DEMO_ENABLED` | Mặc định `false`. Chỉ khi chạy `npm run dev` và đặt `true` mới bật tài khoản demo. Production luôn dùng backend thật |
| `AUTH_SECRET` | Khoá ký cookie phiên đăng nhập của Auth.js v5. Script tự sinh; muốn sinh lại thủ công thì chạy `npx auth secret` |
| `NEXTAUTH_URL` | URL của chính app này, để `http://localhost:3000` khi chạy local |
| `AUTH_GOOGLE_ID` / `AUTH_GOOGLE_SECRET` | Lấy từ Google Cloud Console. Để trống cũng chạy được — nút "Đăng nhập với Google" sẽ tự ẩn |

> Đừng bao giờ commit `.env.local` lên git — file này đã nằm trong `.gitignore` sẵn.

Đăng nhập backend thật bằng **email**. Khi đăng ký, mật khẩu cần ít nhất 8 ký tự, có chữ hoa,
số và ký tự đặc biệt, khớp validator backend. Auth.js giữ refresh token trong JWT cookie,
không đưa refresh token ra session phía trình duyệt. Google chỉ hiển thị khi đủ cấu hình.

Demo chỉ để xem thử giao diện: token `local-token-*` không được gửi sang API, không có quyền
ghi dữ liệu backend. Muốn demo, thêm `AUTH_DEMO_ENABLED=true` vào `.env.local` và chạy lại
`npm run dev`. Không dùng tài khoản demo để kiểm chứng Create/Update/Publish.

Kiểm tra trước khi nộp bài:

```bash
npm run type-check
npm run lint
npm run test
npm run build
npm run test:production-auth
```

Build cần kết nối Google Fonts theo cấu hình `next/font/google` hiện có. Khi deploy, cấp
`AUTH_SECRET` riêng qua môi trường chạy; cấu hình `NEXTAUTH_URL`/`AUTH_URL` đúng domain HTTPS.
Không dùng secret của máy phát triển cho Production. URL `NEXT_PUBLIC_*` được đóng vào bundle
lúc build; URL nội bộ và cờ demo được kiểm tra phía server.

### Cách khác: chạy bằng Docker

Nếu không muốn cài Node.js, dùng `docker-compose.yml` ở thư mục gốc repo:

```bash
docker compose up --build frontend
```

Cách này build ở chế độ production (`next build` + `next start`), nên **không có hot-reload** —
sửa code phải build lại. Để vừa code vừa xem thay đổi ngay thì dùng `npm run dev` ở trên.

## Cấu trúc thư mục

```
frontend/
├── app/                    # Next.js App Router — mỗi route là 1 folder có page.tsx
│   ├── (public)/            # Route công khai: /, /recipes, /categories, /search
│   ├── (auth)/               # /auth/login, /auth/register
│   ├── dashboard/            # Route yêu cầu đăng nhập (Author/Admin)
│   ├── profile/              # Trang hồ sơ cá nhân
│   ├── api/auth/[...nextauth]/  # Route handler của Auth.js
│   ├── layout.tsx            # Root layout (Header/Footer/Providers)
│   └── globals.css
├── components/
│   ├── ui/                  # Component nguyên tử tái dùng (Button, Input, Card...)
│   ├── layout/               # Header, Footer
│   ├── recipe/                # RecipeCard, RecipeGrid, IngredientList...
│   ├── category/              # CategoryCard, CategoryGrid
│   └── forms/                 # LoginForm, RegisterForm, RecipeForm
├── hooks/                   # TanStack Query hooks (useRecipes, useCategories...)
├── lib/
│   ├── api-client.ts         # Hàm fetch dùng chung, tự gắn Bearer token + parse lỗi RFC7807
│   ├── utils.ts               # Hàm tiện ích (cn, formatDate...)
│   └── validations/            # Zod schema cho từng form
├── types/                   # Type TypeScript khớp DTO backend (mục 8 API spec)
├── public/                  # Static asset (favicon...)
├── auth.ts                  # Cấu hình Auth.js v5 (providers, callbacks)
├── middleware.ts             # Chặn truy cập /dashboard, /profile khi chưa đăng nhập
└── Dockerfile                # Build production image (multi-stage, output: standalone)
```

## Hệ thống giao diện & hiệu ứng

Toàn bộ hiệu ứng động chạy bằng **CSS thuần + vài dòng JavaScript**, không cài thêm thư viện
animation nào (framer-motion, GSAP...) — bundle nhẹ và intern dễ đọc hơn.

### Nơi khai báo

| File | Chứa gì |
|---|---|
| `tailwind.config.ts` | Bảng màu (`brand`/`spice`/`herb`/`cream`), shadow ấm, và "thư viện" keyframes: `float`, `shine`, `steam`, `fade-up`, `gradient-x`, `marquee`, `pulse-ring`, `pop`, `wiggle`, `shimmer`, `spin-slow` |
| `app/globals.css` | Class dùng chung: `.hover-lift`, `.link-underline`, `.shine-on-hover`, `.glass`, `.text-gradient`, `.skeleton`, `.container-page` |

Dùng hiệu ứng = thêm class, ví dụ `className="animate-float"` hoặc `className="hover-lift"`.

### Component hiệu ứng

| Component | Tác dụng |
|---|---|
| `ui/Reveal.tsx` | Nội dung hiện dần + trượt lên khi cuộn tới (IntersectionObserver). Truyền `delay` để các card hiện lần lượt |
| `ui/TiltCard.tsx` | Card nghiêng 3D theo con trỏ chuột + vầng sáng bám theo chuột |
| `ui/CursorGlow.tsx` | Vầng sáng cam bám theo chuột trên toàn trang (tự ẩn trên thiết bị cảm ứng) |
| `ui/ScrollProgress.tsx` | Thanh gradient chạy ngang trên đỉnh trang theo mức cuộn |
| `ui/CountUp.tsx` | Số đếm từ 0 lên giá trị đích khi cuộn tới |
| `home/ChefIllustration.tsx` | Hình đầu bếp + món ăn vẽ bằng SVG, có hơi nóng bốc lên và món ăn trôi nổi |
| `home/HeroSection.tsx` | Khu đầu trang chủ: parallax theo chuột, spotlight, chip món ăn bay |
| `layout/PageHero.tsx` | Dải tiêu đề gradient cho các trang bên trong |
| `layout/AuthShell.tsx` | Khung 2 cột cho trang đăng nhập / đăng ký |

### Hai quy tắc quan trọng khi thêm hiệu ứng

1. **Không dùng `setState` trong `onMouseMove`.** Chuột di chuyển bắn ra hàng chục sự kiện mỗi
   giây; gọi `setState` mỗi lần sẽ khiến React render lại liên tục và trang bị giật. Thay vào đó
   ghi thẳng vào `element.style.transform` (xem `TiltCard.tsx`, `HeroSection.tsx`).

2. **Không gộp parallax và CSS animation lên cùng một thẻ.** CSS animation luôn "thắng" inline
   style, nên nếu một thẻ vừa có `animate-float` vừa bị JS ghi `transform` thì parallax sẽ bị
   nuốt mất. Cách làm đúng: bọc 2 lớp — lớp ngoài nhận `data-depth` cho parallax, lớp trong giữ
   class `animate-*` (xem `ChefIllustration.tsx`).

Mọi hiệu ứng đều tự tắt khi người dùng bật **"giảm chuyển động"** trong cài đặt hệ điều hành —
phần xử lý nằm ở cuối `app/globals.css` (`@media (prefers-reduced-motion: reduce)`). Đây là yêu
cầu về accessibility, đừng xoá.

### Font

`app/layout.tsx` nạp 2 font Google qua `next/font/google`: **Inter** (chữ thường) và
**Playfair Display** (tiêu đề h1/h2/h3, tạo chất "tạp chí ẩm thực"). Next.js tải font lúc build
rồi tự host, nên lần build đầu tiên **cần có mạng**. Nếu máy không vào được Google Fonts, build
sẽ báo lỗi — khi đó xoá 2 khối `Inter(...)`/`Playfair_Display(...)` trong `app/layout.tsx` và bỏ
`className` trên thẻ `<html>`, giao diện sẽ tự lùi về font hệ thống (vẫn chạy bình thường).

## Quy ước code cho intern

- Mọi gọi API đều đi qua `lib/api-client.ts`, KHÔNG gọi `fetch` trực tiếp trong component.
- Mọi dữ liệu lấy từ server (recipes, categories...) đều qua hook trong `hooks/`, dùng TanStack Query —
  không tự quản lý loading/error state bằng `useState` thủ công.
- Mọi form đều dùng `react-hook-form` + schema Zod tương ứng trong `lib/validations/`.
- Component trong `components/ui/` không được biết gì về nghiệp vụ CulinaryBlog (không import type Recipe...),
  chỉ nhận props thuần — để tái dùng được ở bất kỳ trang nào.
- Route trong `app/dashboard/**` và `app/profile/**` được bảo vệ tự động bởi `middleware.ts`,
  không cần tự kiểm tra đăng nhập lặp lại trong từng page.

## Danh sách route đã code (khớp mục 9 — Sitemap Frontend)

| Route | Rendering | Trạng thái |
|---|---|---|
| `/` (trang chủ) | ISR (revalidate 1h) | ✅ Đã code |
| `/recipes` | SSR | ✅ Đã code |
| `/recipes/[slug]` | ISR (revalidate 5p) | ✅ Đã code |
| `/categories` | ISR (revalidate 1h) | ✅ Đã code |
| `/categories/[slug]` | ISR (revalidate 10p) | ✅ Đã code |
| `/search` | SSR | ✅ Đã code |
| `/auth/login`, `/auth/register` | CSR | ✅ Đã code |
| `/dashboard`, `/dashboard/recipes(/new)` | CSR (bảo vệ auth) | ✅ Đã code |
| `/dashboard/recipes/[id]/edit` | CSR (bảo vệ auth) | ✅ Đã code |
| `/dashboard/categories` | CSR (bảo vệ auth, Admin only) | ✅ Đã code |
| `/profile` | CSR (bảo vệ auth) | ✅ Đã code |

**Gợi ý thứ tự chạy thử cho intern:** vì backend cần thời gian hoàn thiện dần từng nhóm
endpoint (mục 8), nên chạy `npm run dev` rồi test **trang chủ `/` trước** (chỉ cần API
`GET /recipes` và `GET /categories` là chạy được), các route còn lại (auth, dashboard...) cứ
để đó dùng dần khi backend có endpoint tương ứng — code đã sẵn, không cần viết lại.

## Docker

`Dockerfile` build theo kiểu multi-stage, dùng `output: "standalone"` (đã bật trong
`next.config.ts`) để image gọn nhẹ:

```bash
docker build -t culinary-blog-frontend --build-arg NEXT_PUBLIC_API_BASE_URL=http://localhost:5000/api/v1 .
docker run -p 3000:3000 culinary-blog-frontend
```

## Trạng thái hiện tại

Đây là bản khung (scaffold) đầy đủ cấu trúc + UI theo đúng sitemap mục 9, gọi API thật qua
`lib/api-client.ts`. Vì backend đang trong quá trình xây dựng nên một số màn hình sẽ hiện
trạng thái lỗi/loading cho đến khi các endpoint tương ứng (mục 8) hoàn thiện — đó là hành vi
mong đợi, không phải bug.

**Quan trọng:** phần frontend này được viết tay trong môi trường sandbox bị chặn truy cập
`registry.npmjs.org`, nên CHƯA chạy được `npm install` / `npm run build` / `npm run type-check`
để tự kiểm tra. Sau khi copy code về máy, việc đầu tiên cần làm là:

```bash
npm install
npm run type-check
npm run dev
```

Nếu gặp lỗi TypeScript hoặc lỗi runtime, báo lại nội dung lỗi để được sửa tiếp trong lượt sau.
