import { FlatCompat } from "@eslint/eslintrc";

const compat = new FlatCompat({
  baseDirectory: import.meta.dirname,
});

/**
 * Cấu hình ESLint tối thiểu, kế thừa bộ rule chuẩn của Next.js.
 * (mục 5.5 NFR: "Frontend: ESLint Airbnb ruleset" — nhóm có thể mở rộng thêm
 * eslint-config-airbnb sau khi npm install; để bộ mặc định trước cho gọn.)
 */
const eslintConfig = [{ ignores: [".next/**", "out/**", "build/**", "node_modules/**", "next-env.d.ts"] },
  ...compat.extends("next/core-web-vitals", "next/typescript")];

export default eslintConfig;
