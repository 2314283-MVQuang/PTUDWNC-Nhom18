// Chạy sau npm run build. Backend giả lập và Next.js chỉ nghe localhost, không dùng DB nhóm.
import { createServer } from "node:http";
import { spawn } from "node:child_process";
import { randomBytes, createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import assert from "node:assert/strict";

const profile = { id: "backend-test-author", email: "backend@example.test", displayName: "Backend author", avatarUrl: null, bio: null, roles: ["Author"] };
const tokens = { accessToken: "backend-test-access-token", refreshToken: "backend-test-refresh-token", expiresIn: 900, user: profile };
const calls = [];
const backend = createServer(async (request, response) => {
  let raw = "";
  for await (const chunk of request) raw += chunk;
  const body = raw ? JSON.parse(raw) : undefined;
  calls.push({ path: request.url, body });
  response.setHeader("Content-Type", "application/json");
  if (request.url === "/api/v1/auth/register" || (request.url === "/api/v1/auth/login" && body.email === profile.email))
    response.end(JSON.stringify({ data: tokens }));
  else if (request.url === "/api/v1/auth/me") response.end(JSON.stringify({ data: profile }));
  else { response.statusCode = 401; response.end(JSON.stringify({ type: "UNAUTHORIZED", title: "Unauthorized", status: 401 })); }
});
await new Promise(resolve => backend.listen(0, "127.0.0.1", resolve));
const reservation = createServer();
await new Promise(resolve => reservation.listen(0, "127.0.0.1", resolve));
const port = reservation.address().port;
await new Promise(resolve => reservation.close(resolve));
const base = `http://127.0.0.1:${port}`;
const before = createHash("sha256").update(await readFile(new URL("../data/users.json", import.meta.url))).digest("hex");
let output = "";
const next = spawn(process.execPath, [".next/standalone/server.js"], {
  cwd: new URL("..", import.meta.url), windowsHide: true,
  env: { ...process.env, NODE_ENV: "production", AUTH_DEMO_ENABLED: "true", AUTH_SECRET: randomBytes(32).toString("base64"),
    AUTH_URL: base, NEXTAUTH_URL: base, AUTH_TRUST_HOST: "true", HOSTNAME: "127.0.0.1", PORT: String(port),
    API_INTERNAL_BASE_URL: `http://127.0.0.1:${backend.address().port}/api/v1` },
  stdio: ["ignore", "pipe", "pipe"],
});
next.stdout.on("data", chunk => { output = (output + chunk).slice(-4000); });
next.stderr.on("data", chunk => { output = (output + chunk).slice(-4000); });
const cookies = new Map();
async function call(path, options = {}) {
  const response = await fetch(base + path, { ...options, redirect: "manual",
    headers: { Cookie: [...cookies].map(([key, value]) => `${key}=${value}`).join("; "), ...options.headers } });
  for (const cookie of response.headers.getSetCookie()) {
    const pair = cookie.split(";", 1)[0]; const index = pair.indexOf("=");
    cookies.set(pair.slice(0, index), pair.slice(index + 1));
  }
  return response;
}
async function login(account) {
  const csrf = await (await call("/api/auth/csrf")).json();
  const response = await call("/api/auth/callback/credentials", { method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded", "X-Auth-Return-Redirect": "1" },
    body: new URLSearchParams({ csrfToken: csrf.csrfToken, account, password: "Admin@123", callbackUrl: `${base}/dashboard` }) });
  return response.json();
}
try {
  let ready = false;
  for (let i = 0; i < 80; i++) {
    if (next.exitCode !== null) throw new Error(output);
    try { if ((await fetch(base + "/api/auth/csrf")).ok) { ready = true; break; } } catch { /* Đợi server khởi động. */ }
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  assert.ok(ready, output);
  const anonymous = await call("/dashboard");
  assert.equal(anonymous.status, 307);
  assert.ok(anonymous.headers.get("location").includes("/login"));
  const page = await call("/login");
  assert.equal(page.status, 200);
  assert.ok(!(await page.text()).includes("Chế độ demo:"));
  const rejected = await login("admin");
  assert.ok(rejected.url.includes("CredentialsSignin"), JSON.stringify(rejected));
  assert.equal((await (await call("/api/auth/session")).json())?.accessToken, undefined);
  const registration = await call("/api/auth/register", { method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ account: "week5author", email: profile.email, password: "Author@123", confirmPassword: "Author@123" }) });
  assert.equal(registration.status, 201);
  assert.ok(calls.some(call => call.path === "/api/v1/auth/register" && call.body.userName === "week5author"));
  const signedIn = await login(profile.email);
  assert.ok(!signedIn.url.includes("error="), JSON.stringify(signedIn));
  const session = await (await call("/api/auth/session")).json();
  assert.equal(session.accessToken, tokens.accessToken);
  assert.equal(session.user.id, profile.id);
  assert.equal(session.isDemo, false);
  assert.equal(session.refreshToken, undefined);
  assert.deepEqual(session.roles, ["Author"]);
  const adminPage = await call("/dashboard/categories");
  assert.equal(adminPage.status, 307);
  assert.ok(adminPage.headers.get("location").endsWith("/dashboard"));
  const after = createHash("sha256").update(await readFile(new URL("../data/users.json", import.meta.url))).digest("hex");
  assert.equal(after, before);
  console.log("PASS: Production tắt demo dù cờ=true; đăng ký/login đi backend; session không lộ refresh token; route bảo vệ đúng; users.json giữ nguyên.");
} catch (error) {
  console.error(output);
  throw error;
} finally {
  next.kill();
  if (next.exitCode === null) await new Promise(resolve => next.once("exit", resolve));
  await new Promise(resolve => backend.close(resolve));
}
