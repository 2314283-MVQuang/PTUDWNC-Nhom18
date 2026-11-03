// Chỉ kiểm tra cấu hình Compose đã resolve; không khởi động container hoặc in secret.
import { spawnSync } from "node:child_process";
import { fileURLToPath, pathToFileURL } from "node:url";
import { resolve } from "node:path";
import { isIP } from "node:net";

export function validateGoogleProductionConfig(compose) {
  const frontend = compose.services?.frontend?.environment ?? {};
  const api = compose.services?.api?.environment ?? {};
  const errors = [];
  const id = frontend.AUTH_GOOGLE_ID;
  const secret = frontend.AUTH_GOOGLE_SECRET;
  if (typeof id !== "string" || !/^[a-z0-9-]+\.apps\.googleusercontent\.com$/i.test(id))
    errors.push("GOOGLE_CLIENT_ID thiếu hoặc sai định dạng OAuth Web Client.");
  if (typeof secret !== "string" || !secret.trim() || /[\r\n]/.test(secret))
    errors.push("GOOGLE_CLIENT_SECRET thiếu hoặc không hợp lệ.");
  if (typeof id !== "string" || api.Google__ClientId !== id)
    errors.push("Google Client ID của API và frontend không khớp.");
  if (api.Google__ClientSecret || api.AUTH_GOOGLE_SECRET)
    errors.push("Client Secret chỉ cần cấp cho server Next.js, không cấp cho API.");
  if (Object.entries(frontend).some(([key, value]) => key.startsWith("NEXT_PUBLIC_")
    && typeof value === "string" && typeof secret === "string" && secret.length > 0 && value.includes(secret)))
    errors.push("Không đặt Google Client Secret trong biến NEXT_PUBLIC_*.");

  let origin;
  try {
    const url = new URL(frontend.AUTH_URL);
    if (url.protocol !== "https:" || url.username || url.password || url.search || url.hash
      || url.pathname !== "/" || url.hostname === "localhost" || isIP(url.hostname.replace(/^\[|\]$/g, ""))
      || !url.hostname.includes("."))
      throw new Error();
    origin = url.origin;
    if (frontend.NEXTAUTH_URL !== frontend.AUTH_URL)
      errors.push("AUTH_URL và NEXTAUTH_URL phải cùng địa chỉ website chung.");
  } catch {
    errors.push("PUBLIC_WEB_URL phải là origin HTTPS công khai, không kèm path/query hoặc thông tin tài khoản.");
  }
  return { errors, callbackUrl: origin ? `${origin}/api/auth/callback/google` : undefined };
}

function main() {
  const root = fileURLToPath(new URL("..", import.meta.url));
  const envFile = process.argv[2] ? resolve(process.argv[2]) : resolve(root, ".env.prod");
  const result = spawnSync("docker", ["compose", "-f", "docker-compose.prod.yml", "--env-file", envFile,
    "config", "--format", "json"], { cwd: root, encoding: "utf8", windowsHide: true });
  if (result.error || result.status !== 0) {
    console.error("Không đọc được cấu hình Compose. Kiểm tra Docker Compose và các biến bắt buộc trong tệp môi trường; không đưa nội dung tệp đó lên GitHub.");
    process.exitCode = 1;
    return;
  }
  let configuration;
  try { configuration = JSON.parse(result.stdout); }
  catch {
    console.error("Compose không trả về cấu hình JSON hợp lệ.");
    process.exitCode = 1;
    return;
  }
  const { errors, callbackUrl } = validateGoogleProductionConfig(configuration);
  if (errors.length) {
    for (const error of errors) console.error(`- ${error}`);
    process.exitCode = 1;
    return;
  }
  console.log("PASS: Google được cấu hình cho website chung; API/frontend dùng cùng Client ID.");
  console.log(`Authorized redirect URI cần khai báo trong Google Console: ${callbackUrl}`);
  console.log("Chưa kiểm chứng Client Secret với Google hoặc đăng nhập bằng tài khoản thật.");
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) main();
