/** Server và Edge kiểm tra cùng điều kiện; Production luôn bỏ qua cờ demo. */
export function isDemoAuthEnabled() {
  return process.env.NODE_ENV === "development" && process.env.AUTH_DEMO_ENABLED === "true";
}

export function isLocalAccessToken(token: string | undefined) {
  return token?.startsWith("local-token-") === true;
}

/** Chỉ cho điều hướng bên trong ứng dụng sau khi đăng nhập. */
export function safeCallbackUrl(value: string | null) {
  return value?.startsWith("/") && !value.startsWith("//") && !/[\\\r\n]/.test(value)
    ? value : "/dashboard";
}
