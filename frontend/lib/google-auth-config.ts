/** Chỉ dùng trong auth.ts và Server Components; chỉ truyền cờ enabled xuống trình duyệt. */
export function getGoogleOAuthConfiguration(environment: Readonly<Record<string, string | undefined>> = process.env) {
  const clientId = environment.AUTH_GOOGLE_ID?.trim();
  const clientSecret = environment.AUTH_GOOGLE_SECRET?.trim();
  return clientId && clientSecret ? { clientId, clientSecret } : null;
}
