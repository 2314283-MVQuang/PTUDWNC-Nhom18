// Chạy sau npm run build. Kiểm thử UI với API giả lập; không sử dụng database nhóm.
// CULINARY_PLAYWRIGHT_MODULE_PATH trỏ tới index.mjs của playwright-core cài riêng ở thư mục tạm.
import { createServer } from "node:http";
import { spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { cp, mkdir } from "node:fs/promises";
import { pathToFileURL, fileURLToPath } from "node:url";
import assert from "node:assert/strict";

if (!process.env.CULINARY_PLAYWRIGHT_MODULE_PATH)
  throw new Error("Đặt CULINARY_PLAYWRIGHT_MODULE_PATH tới index.mjs của playwright-core; xem báo cáo tuần 6.");
const { chromium } = await import(pathToFileURL(process.env.CULINARY_PLAYWRIGHT_MODULE_PATH).href);
const categoryId = "11111111-1111-4111-8111-111111111111";
const recipes = [
  { id: "easy", slug: "pho-bo-nhanh", title: "Phở bò nhanh", description: "Món phở dễ nấu cho bữa sáng gia đình.", prepTime: 15, cookTime: 20, servings: 2, difficulty: "Easy", status: "Published", publishedAt: "2026-10-14T00:00:00Z" },
  { id: "hard", slug: "pho-bo-truyen-thong", title: "Phở bò truyền thống", description: "Nước dùng đậm đà với thời gian ninh lâu hơn.", prepTime: 40, cookTime: 90, servings: 4, difficulty: "Hard", status: "Published", publishedAt: "2026-10-14T00:00:00Z" },
];
const calls = [];
let failSearch = false;
const backend = createServer((request, response) => {
  const url = new URL(request.url, "http://localhost");
  response.setHeader("Content-Type", "application/json; charset=utf-8");
  if (url.pathname === "/api/v1/categories") {
    response.end(JSON.stringify({ data: [{ id: categoryId, name: "Món nước", slug: "mon-nuoc", description: null, imageUrl: null, recipeCount: 2 }] }));
  } else if (url.pathname === "/api/v1/recipes/search") {
    calls.push(Object.fromEntries(url.searchParams));
    if (failSearch) {
      response.statusCode = 503;
      response.end(JSON.stringify({ title: "Dịch vụ tìm kiếm tạm thời gián đoạn.", status: 503 }));
      return;
    }
    const p = url.searchParams;
    const selected = recipes.filter(recipe => {
      if (p.get("q") === "khongtimthay") return false;
      if (p.get("categoryId") && p.get("categoryId") !== categoryId) return false;
      if (p.get("difficulty") && p.get("difficulty") !== recipe.difficulty) return false;
      for (const [key, field, minimum] of [
        ["minPrepTime", "prepTime", true], ["maxPrepTime", "prepTime", false],
        ["minCookTime", "cookTime", true], ["maxCookTime", "cookTime", false],
      ]) if (p.has(key) && (minimum ? recipe[field] < Number(p.get(key)) : recipe[field] > Number(p.get(key)))) return false;
      return true;
    });
    response.end(JSON.stringify({ data: selected }));
  } else { response.statusCode = 404; response.end("{}"); }
});
await new Promise(resolve => backend.listen(0, "127.0.0.1", resolve));
const reservation = createServer();
await new Promise(resolve => reservation.listen(0, "127.0.0.1", resolve));
const port = reservation.address().port;
await new Promise(resolve => reservation.close(resolve));
const base = `http://127.0.0.1:${port}`;
const frontendRoot = new URL("..", import.meta.url);
const evidence = new URL("../../spec/minh_chung_tuan_6/", import.meta.url);
await mkdir(evidence, { recursive: true });
await cp(new URL(".next/static", frontendRoot), new URL(".next/standalone/.next/static", frontendRoot), { recursive: true });
await cp(new URL("public", frontendRoot), new URL(".next/standalone/public", frontendRoot), { recursive: true });
const next = spawn(process.execPath, [".next/standalone/server.js"], {
  cwd: frontendRoot, windowsHide: true, stdio: ["ignore", "pipe", "pipe"],
  env: { ...process.env, NODE_ENV: "production", HOSTNAME: "127.0.0.1", PORT: String(port),
    AUTH_SECRET: randomBytes(32).toString("base64"), AUTH_URL: base, NEXTAUTH_URL: base, AUTH_TRUST_HOST: "true",
    API_INTERNAL_BASE_URL: `http://127.0.0.1:${backend.address().port}/api/v1` },
});
let serverLog = "";
next.stdout.on("data", data => { serverLog = (serverLog + data).slice(-4000); });
next.stderr.on("data", data => { serverLog = (serverLog + data).slice(-4000); });
let browser;
try {
  let ready = false;
  for (let attempt = 0; attempt < 80; attempt++) {
    if (next.exitCode !== null) throw new Error(serverLog);
    try { if ((await fetch(`${base}/api/auth/csrf`)).ok) { ready = true; break; } } catch { /* khởi động */ }
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  assert.ok(ready, "Next server chưa sẵn sàng");
  browser = await chromium.launch({ channel: "msedge", headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, reducedMotion: "reduce" });
  const pageErrors = [];
  page.on("pageerror", error => pageErrors.push(error.message));
  const visibleText = async text => page.getByText(text, { exact: true }).waitFor({ state: "visible" });
  await page.goto(`${base}/search?q=pho`);
  await visibleText("2 công thức hiển thị");
  const form = page.getByRole("complementary", { name: "Bộ lọc tìm kiếm" }).getByRole("form", { name: "Lọc công thức" });
  await form.getByRole("combobox", { name: /^Danh mục/ }).selectOption(categoryId);
  await form.getByRole("combobox", { name: /^Độ khó/ }).selectOption("Easy");
  await form.locator('[name="maxCookTime"]').fill("20");
  await form.getByRole("button", { name: "Áp dụng", exact: true }).click();
  await page.waitForURL(url => url.searchParams.get("difficulty") === "Easy");
  await page.getByText("1 công thức hiển thị", { exact: false }).waitFor();
  assert.ok(calls.some(call => call.categoryId === categoryId && call.difficulty === "Easy" && call.maxCookTime === "20"));
  assert.equal(await page.getByRole("link", { name: "Phở bò truyền thống", exact: true }).count(), 0);
  await page.evaluate(async () => {
    window.scrollTo({ top: 0, behavior: "instant" });
    await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  });
  await page.screenshot({ path: fileURLToPath(new URL("search-desktop.png", evidence)), fullPage: true });
  await form.locator('[name="minPrepTime"]').fill("30");
  await form.locator('[name="maxPrepTime"]').fill("20");
  const beforeInvalid = calls.length;
  await form.getByRole("button", { name: "Áp dụng", exact: true }).click();
  await form.getByRole("alert").waitFor();
  assert.equal(calls.length, beforeInvalid);
  await form.getByRole("button", { name: "Xóa bộ lọc", exact: true }).click();
  await page.waitForURL(url => !url.searchParams.has("difficulty"));
  await visibleText("2 công thức hiển thị");
  await page.goBack();
  await page.waitForURL(url => url.searchParams.get("difficulty") === "Easy");
  await page.waitForFunction(() => document.querySelector('aside select[name="difficulty"]')?.value === "Easy");
  await page.goForward();
  await page.waitForFunction(() => document.querySelector('aside select[name="difficulty"]')?.value === "");

  await page.setViewportSize({ width: 390, height: 844 });
  const trigger = page.getByRole("button", { name: "Tìm kiếm và bộ lọc", exact: true });
  await trigger.click();
  const dialog = page.getByRole("dialog", { name: "Tìm kiếm và bộ lọc" });
  await dialog.waitFor();
  await dialog.getByRole("combobox", { name: /^Độ khó/ }).selectOption("Hard");
  await page.screenshot({ path: fileURLToPath(new URL("search-mobile.png", evidence)) });
  for (let i = 0; i < 14; i++) {
    await page.keyboard.press("Tab");
    assert.ok(await dialog.evaluate(element => element.contains(document.activeElement)), "Focus phải nằm trong dialog");
  }
  await page.keyboard.press("Escape");
  await dialog.waitFor({ state: "hidden" });
  assert.ok(await trigger.evaluate(element => element === document.activeElement));
  await trigger.click();
  assert.equal(await dialog.getByRole("combobox", { name: /^Độ khó/ }).inputValue(), "Hard");
  await dialog.getByRole("button", { name: "Áp dụng", exact: true }).click();
  await page.waitForURL(url => url.searchParams.get("difficulty") === "Hard");
  await page.getByRole("link", { name: "Phở bò truyền thống", exact: true }).waitFor();
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), "Không tràn ngang mobile");

  await page.goto(`${base}/search?q=khongtimthay`);
  await visibleText("Chưa tìm thấy công thức phù hợp");
  await page.screenshot({ path: fileURLToPath(new URL("search-empty.png", evidence)), fullPage: true });
  const beforeUrlInvalid = calls.length;
  await page.goto(`${base}/search?q=pho&minCookTime=60&maxCookTime=10`);
  await visibleText("Bộ lọc chưa hợp lệ");
  assert.equal(calls.length, beforeUrlInvalid);
  failSearch = true;
  await page.goto(`${base}/search?q=pho`);
  await visibleText("Dịch vụ tìm kiếm tạm thời gián đoạn.");
  await page.screenshot({ path: fileURLToPath(new URL("search-error.png", evidence)), fullPage: true });
  failSearch = false;
  await page.getByRole("button", { name: "Thử lại", exact: true }).click();
  await visibleText("2 công thức hiển thị");
  assert.deepEqual(pageErrors, [], "Không có lỗi JavaScript runtime");
  console.log("PASS: desktop/mobile, kết hợp filters, validation, reset, Back/Forward, focus trap/Escape, empty/error/retry; không tràn ngang và không có pageerror. API giả lập; PostgreSQL được kiểm tra riêng.");
} finally {
  await browser?.close();
  next.kill();
  backend.closeAllConnections();
  await new Promise(resolve => backend.close(resolve));
}
