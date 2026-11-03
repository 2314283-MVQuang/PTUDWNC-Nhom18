// Cùng một build Production: chỉ bật Google bằng biến môi trường lúc chạy, dùng khóa GIẢ.
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { createServer } from "node:net";
import { cp, mkdir, writeFile } from "node:fs/promises";

const root = new URL("..", import.meta.url);
await cp(new URL(".next/static", root), new URL(".next/standalone/.next/static", root), { recursive: true });
await cp(new URL("public", root), new URL(".next/standalone/public", root), { recursive: true });
const result = [];
for (const mode of ["disabled", "partial", "enabled"]) {
  const reservation = createServer();
  await new Promise(resolve => reservation.listen(0, "127.0.0.1", resolve));
  const port = reservation.address().port;
  await new Promise(resolve => reservation.close(resolve));
  const base = `http://127.0.0.1:${port}`;
  const secret = `test-only-${randomBytes(24).toString("hex")}`;
  const server = spawn(process.execPath, [".next/standalone/server.js"], {
    cwd: root, windowsHide: true, stdio: ["ignore", "pipe", "pipe"],
    env: { ...process.env, NODE_ENV: "production", HOSTNAME: "127.0.0.1", PORT: String(port),
      AUTH_URL: base, NEXTAUTH_URL: base, AUTH_TRUST_HOST: "true", AUTH_DEMO_ENABLED: "false",
      AUTH_SECRET: randomBytes(32).toString("base64"),
      AUTH_GOOGLE_ID: mode === "disabled" ? "" : "123-runtime-test.apps.googleusercontent.com",
      AUTH_GOOGLE_SECRET: mode === "enabled" ? secret : "",
    },
  });
  let output = "";
  server.stdout.on("data", value => { output = (output + value).slice(-10000); });
  server.stderr.on("data", value => { output = (output + value).slice(-10000); });
  try {
    let ready = false;
    for (let attempt = 0; attempt < 100; attempt++) {
      if (server.exitCode !== null) throw new Error(`Next.js dừng sớm ở chế độ ${mode}.`);
      try { if ((await fetch(base + "/api/auth/providers")).ok) { ready = true; break; } }
      catch { /* Chờ tiến trình Next.js vừa được script tạo. */ }
      await new Promise(resolve => setTimeout(resolve, 200));
    }
    assert.ok(ready, "Next.js không sẵn sàng.");
    const response = await fetch(base + "/api/auth/providers");
    const providersText = await response.text();
    const providers = JSON.parse(providersText);
    assert.ok(providers.credentials);
    assert.equal(!!providers.google, mode === "enabled");
    const page = await fetch(base + "/login");
    assert.equal(page.status, 200);
    const html = await page.text();
    assert.equal(html.includes("Đăng nhập với Google"), mode === "enabled");
    const session = await (await fetch(base + "/api/auth/session")).text();
    for (const text of [providersText, html, session, output])
      assert.ok(!text.includes(secret), "Client Secret xuất hiện trong response hoặc log.");
    result.push({ mode, passed: true, googleProvider: !!providers.google, secretInResponsesOrLogs: false });
    console.log(`PASS: Google ${mode}; provider và nút đăng nhập đồng bộ, secret không lộ trong response/log.`);
  } finally {
    server.kill();
    if (server.exitCode === null) await new Promise(resolve => server.once("exit", resolve));
  }
}
const evidence = new URL("../../spec/minh_chung_google_2026_11_03/", import.meta.url);
await mkdir(evidence, { recursive: true });
await writeFile(new URL("runtime.json", evidence), JSON.stringify({
  date: "2026-11-03", mode: "Production standalone", sameBuild: true, cases: result,
  realGoogleLoginVerified: false,
  note: "Chỉ dùng khóa giả để kiểm tra cấu hình runtime và phản hồi. Chưa trao đổi code/token với Google hoặc đăng nhập tài khoản thật.",
}, null, 2) + "\n", "utf8");
