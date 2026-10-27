// UI dùng API giả lập riêng trên localhost. Backend/SMTP thật được kiểm tra bởi script .NET.
import { createServer } from "node:http";
import { spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { cp, mkdir } from "node:fs/promises";
import { pathToFileURL, fileURLToPath } from "node:url";
import assert from "node:assert/strict";

if (!process.env.CULINARY_PLAYWRIGHT_MODULE_PATH) throw new Error("Đặt CULINARY_PLAYWRIGHT_MODULE_PATH tới playwright-core/index.mjs.");
const { chromium } = await import(pathToFileURL(process.env.CULINARY_PLAYWRIGHT_MODULE_PATH).href);
const categoryId = "11111111-1111-4111-8111-111111111111";
const categories = [{ id: categoryId, slug: "mon-nuoc", name: "Món nước", imageUrl: null, recipeCount: 2 }];
const recipes = [
  { id: "a", slug: "pho-nhanh", title: "Phở bò nhanh", description: "Công thức phở cho gia đình", prepTime: 15, cookTime: 20, servings: 2, difficulty: "Easy" },
  { id: "b", slug: "pho-cham", title: "Phở bò truyền thống", description: "Công thức phở ninh lâu", prepTime: 30, cookTime: 90, servings: 4, difficulty: "Hard" },
].map(recipe => ({ ...recipe, status: "Published", categoryName: "Món nước", authorDisplayName: "Tác giả", primaryImageUrl: null, publishedAt: "2026-10-27T00:00:00Z" }));
const users = new Map(); const accesses = new Map(); const calls = []; let serial = 0;
let failCategories = true; let failRecipes = true; let failDetails = true; let resetEmail = "";
const resetToken = "a+b/c==";
function tokens(user) {
  const accessToken = `ui-access-${++serial}`; const refreshToken = `ui-refresh-${serial}`;
  accesses.set(accessToken, user);
  return { accessToken, refreshToken, expiresIn: 900, user: user.profile };
}
const backend = createServer(async (request, response) => {
  try {
    response.setHeader("Content-Type", "application/json; charset=utf-8");
    response.setHeader("Access-Control-Allow-Origin", "*");
    if (request.method === "OPTIONS") { response.statusCode = 204; response.end(); return; }
    const url = new URL(request.url, "http://localhost");
    let raw = ""; for await (const chunk of request) raw += chunk;
    const body = raw ? JSON.parse(raw) : {};
    calls.push({ path: url.pathname, method: request.method, body });
    const ok = (data, meta) => response.end(JSON.stringify({ data, ...(meta && { meta }) }));
    const error = (status, detail) => { response.statusCode = status; response.end(JSON.stringify({ title: detail, detail, status })); };
    const user = accesses.get(request.headers.authorization?.replace(/^Bearer /, ""));
    if (url.pathname === "/api/v1/categories") return failCategories ? error(503, "Danh mục tạm ngừng") : ok(categories);
    if (url.pathname === "/api/v1/recipes") return failRecipes ? error(503, "Danh sách tạm ngừng") : ok(recipes, { page: 1, pageSize: 8, total: 2, totalPages: 1 });
    if (url.pathname === "/api/v1/recipes/search") return ok(recipes.filter(recipe => (!url.searchParams.has("difficulty") || recipe.difficulty === url.searchParams.get("difficulty"))
      && (!url.searchParams.has("maxCookTime") || recipe.cookTime <= Number(url.searchParams.get("maxCookTime")))));
    if (url.pathname === "/api/v1/recipes/temporary-error" && failDetails) return error(503, "Chi tiết tạm ngừng");
    if (url.pathname.startsWith("/api/v1/recipes/")) {
      const recipe = url.pathname === "/api/v1/recipes/temporary-error" ? recipes[0]
        : recipes.find(recipe => url.pathname.endsWith("/" + recipe.slug));
      if (!recipe) return error(404, "Không tìm thấy");
      return ok({ ...recipe, description: 'Phở </script><script>globalThis.recipeInjected=true</script>',
        categoryId, authorId: "author", instructions: "Hướng dẫn thêm cho món phở", rowVersion: "AQ==",
        nutrition: { calories: 420, protein: null, carbohydrates: null, fat: null, fiber: null, sodium: null }, images: [],
        steps: [{ id: "s", stepNumber: 1, title: "Ninh nước dùng", description: "Ninh lửa nhỏ", timerMinutes: null, imageUrl: null }],
        ingredients: [{ id: "i", name: "Thịt bò", quantity: 200, unit: "g", notes: null, orderIndex: 0 }] });
    }
    if (url.pathname === "/api/v1/auth/register") {
      if (users.has(body.email)) return error(409, "Email đã được sử dụng.");
      const registered = { password: body.password, profile: { id: `author-${users.size}`, email: body.email, displayName: body.fullName,
        avatarUrl: null, bio: null, roles: ["Author"] } };
      users.set(body.email, registered); response.statusCode = 201; return ok(tokens(registered));
    }
    if (url.pathname === "/api/v1/auth/login") {
      const registered = users.get(body.email);
      return registered?.password === body.password ? ok(tokens(registered)) : error(401, "Email hoặc mật khẩu không đúng.");
    }
    if (url.pathname === "/api/v1/auth/forgot-password") { resetEmail = body.email; response.statusCode = 204; response.end(); return; }
    if (url.pathname === "/api/v1/auth/reset-password") {
      if (body.token !== resetToken || !users.has(body.email)) return error(401, "Yêu cầu đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
      users.get(body.email).password = body.newPassword; response.statusCode = 204; response.end(); return;
    }
    if (!user) return error(401, "Chưa đăng nhập");
    if (url.pathname === "/api/v1/auth/me") {
      if (request.method === "PATCH") for (const field of ["displayName", "bio", "avatarUrl"]) {
        if (body[field] !== undefined) user.profile[field] = body[field] || null;
      }
      return ok(user.profile);
    }
    if (url.pathname === "/api/v1/auth/change-password") {
      if (body.currentPassword !== user.password) return error(422, "Mật khẩu hiện tại chưa đúng.");
      user.password = body.newPassword; response.statusCode = 204; response.end(); return;
    }
    if (url.pathname === "/api/v1/auth/logout") { response.statusCode = 204; response.end(); return; }
    error(404, "Không có endpoint");
  } catch { response.statusCode = 500; response.end("{}"); }
});
await new Promise(resolve => backend.listen(0, "127.0.0.1", resolve));
const backendBase = `http://127.0.0.1:${backend.address().port}`;
const reservation = createServer(); await new Promise(resolve => reservation.listen(0, "127.0.0.1", resolve));
const port = reservation.address().port; await new Promise(resolve => reservation.close(resolve));
const base = `http://127.0.0.1:${port}`;
const frontendRoot = new URL("..", import.meta.url);
const evidence = new URL("../../spec/minh_chung_giao_dien_2026_10_27/", import.meta.url);
await mkdir(evidence, { recursive: true });
await cp(new URL(".next/static", frontendRoot), new URL(".next/standalone/.next/static", frontendRoot), { recursive: true });
await cp(new URL("public", frontendRoot), new URL(".next/standalone/public", frontendRoot), { recursive: true });
const next = spawn(process.execPath, [".next/standalone/server.js"], { cwd: frontendRoot, windowsHide: true, stdio: ["ignore", "pipe", "pipe"],
  env: { ...process.env, NODE_ENV: "production", HOSTNAME: "127.0.0.1", PORT: String(port), AUTH_DEMO_ENABLED: "true",
    AUTH_SECRET: randomBytes(32).toString("base64"), AUTH_URL: base, NEXTAUTH_URL: base, AUTH_TRUST_HOST: "true",
    API_INTERNAL_BASE_URL: backendBase + "/api/v1" } });
let log = ""; next.stdout.on("data", data => { log = (log + data).slice(-5000); }); next.stderr.on("data", data => { log = (log + data).slice(-5000); });
let browser; let page;
try {
  let ready = false;
  for (let attempt = 0; attempt < 100; attempt++) {
    if (next.exitCode !== null) throw new Error(log);
    try { if ((await fetch(base + "/api/auth/csrf")).ok) { ready = true; break; } } catch { /* Đợi host test. */ }
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  assert.ok(ready, log);
  browser = await chromium.launch({ channel: "msedge", headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: "reduce" });
  page = await context.newPage(); const pageErrors = [];
  page.on("pageerror", error => pageErrors.push(error.message));
  await page.route("**/api/v1/**", async route => {
    const request = route.request(); const url = new URL(request.url());
    const headers = { ...request.headers() }; delete headers.host;
    const response = await fetch(backendBase + url.pathname + url.search, { method: request.method(), headers,
      body: ["GET", "HEAD"].includes(request.method()) ? undefined : request.postData() });
    await route.fulfill({ status: response.status, contentType: "application/json", body: await response.text(), headers: { "Access-Control-Allow-Origin": "*" } });
  });
  await page.route("https://example.test/**", route => route.fulfill({ contentType: "image/svg+xml", body: '<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32"><circle cx="16" cy="16" r="16" fill="orange"/></svg>' }));
  const visible = locator => locator.waitFor({ state: "visible", timeout: 15000 });
  const goto = path => page.goto(base + path);
  async function screenshot(name) {
    // Cho các component Reveal đi qua viewport trước khi chụp toàn trang.
    const height = await page.evaluate(() => document.documentElement.scrollHeight);
    for (let y = 0; y < height; y += 500) {
      await page.evaluate(top => scrollTo(0, top), y);
      await new Promise(resolve => setTimeout(resolve, 100));
    }
    await page.evaluate(() => scrollTo(0, 0));
    await new Promise(resolve => setTimeout(resolve, 800));
    await page.screenshot({ path: fileURLToPath(new URL(name, evidence)), fullPage: true });
  }

  await goto("/categories");
  await visible(page.getByText("Không tải được danh mục. Vui lòng thử lại.", { exact: true }));
  assert.equal(await page.getByText("Chưa có danh mục nào", { exact: true }).count(), 0);
  failCategories = false;
  await page.getByRole("button", { name: "Thử lại" }).click();
  await visible(page.getByRole("heading", { name: "Món nước", exact: true }));
  await goto("/");
  await visible(page.getByRole("complementary", { name: "Danh mục công thức" }).getByRole("link", { name: /Món nước/ }));
  await visible(page.getByText("Chưa tải được công thức mới. Vui lòng thử lại.", { exact: true }));
  failRecipes = false; await page.getByRole("button", { name: "Thử lại" }).click();
  await visible(page.getByRole("heading", { name: recipes[0].title, exact: true }));
  assert.equal(await page.locator("header").count(), 1); assert.equal(await page.locator("footer").count(), 1);
  await screenshot("home-desktop.png");
  await page.setViewportSize({ width: 390, height: 844 });
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), "Home overflows on mobile");
  await screenshot("home-mobile.png");
  await page.setViewportSize({ width: 1440, height: 1000 });

  await goto("/recipes/pho-nhanh");
  await visible(page.getByRole("heading", { name: "Các bước thực hiện", exact: true }));
  await visible(page.getByText("Hướng dẫn thêm cho món phở", { exact: true }));
  assert.equal(await page.evaluate(() => globalThis.recipeInjected), undefined);
  assert.equal(await page.locator('script[type="application/ld+json"]').count(), 1);
  await goto("/recipes/missing"); await visible(page.getByRole("heading", { name: "Không tìm thấy công thức", exact: true }));
  await goto("/recipes/temporary-error"); await visible(page.getByText("Nội dung đang tạm thời gián đoạn. Vui lòng thử lại.", { exact: true }));
  failDetails = false;
  await page.locator("main").getByRole("button", { name: "Thử lại", exact: true }).click();
  await visible(page.getByRole("heading", { name: "Các bước thực hiện", exact: true }));
  await goto(`/search?q=pho&categoryId=${categoryId}&difficulty=Easy&maxCookTime=30`);
  await visible(page.getByRole("heading", { name: recipes[0].title, exact: true }));
  assert.equal(await page.getByRole("heading", { name: recipes[1].title, exact: true }).count(), 0);

  await goto("/profile/change-password"); await page.waitForURL("**/login?**");
  await goto("/register?callbackUrl=%2Fprofile");
  await page.getByLabel("Tên tài khoản", { exact: true }).fill("authorui");
  await page.getByLabel("Email đăng ký", { exact: true }).fill("author+ui@example.test");
  await page.getByLabel("Mật khẩu", { exact: true }).fill("Author@123");
  await page.getByLabel("Xác nhận lại mật khẩu", { exact: true }).fill("Author@123");
  await page.getByRole("button", { name: "Xác nhận đăng ký", exact: true }).click();
  await page.waitForURL(base + "/profile", { timeout: 15000 });
  await page.getByLabel("Tên hiển thị", { exact: true }).fill("Tên cập nhật trên Header");
  await page.getByLabel("Giới thiệu bản thân", { exact: true }).fill("Giới thiệu kiểm thử");
  await page.getByLabel("URL ảnh đại diện", { exact: true }).fill("https://example.test/avatar.png");
  await page.getByRole("button", { name: "Lưu thay đổi" }).click();
  await visible(page.locator("header").getByText("Tên cập nhật trên Header", { exact: true }));
  await visible(page.locator('header a[href="/profile"] img'));
  await page.getByLabel("Giới thiệu bản thân", { exact: true }).fill("");
  await page.getByLabel("URL ảnh đại diện", { exact: true }).fill("");
  await page.getByRole("button", { name: "Lưu thay đổi" }).click();
  await page.locator('header a[href="/profile"] img').waitFor({ state: "detached" });
  assert.equal(users.get("author+ui@example.test").profile.bio, null);
  assert.equal(users.get("author+ui@example.test").profile.avatarUrl, null);
  await screenshot("profile-updated.png");

  await page.getByRole("link", { name: "Đổi mật khẩu", exact: true }).click();
  await page.getByLabel("Mật khẩu hiện tại", { exact: true }).fill("Wrong@123");
  await page.getByLabel("Mật khẩu mới", { exact: true }).fill("Changed@456");
  await page.getByLabel("Xác nhận mật khẩu mới", { exact: true }).fill("Mismatch@456");
  await page.getByRole("button", { name: "Đổi mật khẩu", exact: true }).click();
  await visible(page.getByText("Mật khẩu xác nhận không khớp", { exact: true }));
  await page.getByLabel("Xác nhận mật khẩu mới", { exact: true }).fill("Changed@456");
  await page.getByRole("button", { name: "Đổi mật khẩu", exact: true }).click();
  await visible(page.getByText("Mật khẩu hiện tại chưa đúng.", { exact: true }));
  await page.getByLabel("Mật khẩu hiện tại", { exact: true }).fill("Author@123");
  await page.getByRole("button", { name: "Đổi mật khẩu", exact: true }).click();
  await visible(page.getByText("Đã đổi mật khẩu thành công.", { exact: true }));
  await page.getByRole("button", { name: "Đăng xuất", exact: true }).click();
  // Auth.js chuẩn hóa loopback 127.0.0.1 thành localhost khi redirect đăng xuất.
  await page.waitForURL(url => url.pathname === "/" && url.port === String(port));
  assert.ok(calls.some(call => call.path === "/api/v1/auth/logout" && call.body.refreshToken));
  async function login(email, password, succeeds = true) {
    await goto("/login?callbackUrl=%2Fprofile");
    await page.getByLabel("Email", { exact: true }).fill(email);
    await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
    await page.locator("main form").getByRole("button", { name: "Đăng nhập", exact: true }).click();
    if (succeeds) await page.waitForURL(base + "/profile");
    else await visible(page.getByText("Tài khoản hoặc mật khẩu không chính xác.", { exact: true }));
  }
  await login("author+ui@example.test", "Author@123", false);
  await login("author+ui@example.test", "Changed@456");
  await goto("/forgot-password"); await page.getByLabel("Email", { exact: true }).fill("author+ui@example.test");
  await page.getByRole("button", { name: "Gửi hướng dẫn đặt lại mật khẩu" }).click();
  await visible(page.getByText("Nếu email đã đăng ký, hướng dẫn đặt lại mật khẩu sẽ được gửi đến hộp thư.", { exact: true }));
  assert.equal(resetEmail, "author+ui@example.test");
  await goto("/reset-password"); await visible(page.getByText("Liên kết đặt lại mật khẩu không đầy đủ. Hãy yêu cầu một liên kết mới.", { exact: true }));
  await goto("/reset-password?" + new URLSearchParams({ email: resetEmail, token: resetToken }));
  await page.getByLabel("Mật khẩu mới", { exact: true }).fill("Reset@789");
  await page.getByLabel("Xác nhận mật khẩu mới", { exact: true }).fill("Reset@789");
  await page.getByRole("button", { name: "Đặt lại mật khẩu", exact: true }).click();
  await visible(page.getByText("Đã đặt lại mật khẩu. Có thể đăng nhập bằng mật khẩu mới.", { exact: true }));
  assert.ok(calls.some(call => call.path === "/api/v1/auth/reset-password" && call.body.token === resetToken));
  await login(resetEmail, "Reset@789");
  await visible(page.getByLabel("Tên hiển thị", { exact: true }));
  assert.equal(await page.getByLabel("Tên hiển thị", { exact: true }).inputValue(), "Tên cập nhật trên Header");
  // Đổi tài khoản bằng đăng ký/đăng nhập client-side, giữ QueryClient để bắt lỗi cache chéo user.
  await page.locator("footer").getByRole("link", { name: "Đăng ký", exact: true }).click();
  await page.getByLabel("Tên tài khoản", { exact: true }).fill("secondauthor");
  await page.getByLabel("Email đăng ký", { exact: true }).fill("second@example.test");
  await page.getByLabel("Mật khẩu", { exact: true }).fill("Second@123");
  await page.getByLabel("Xác nhận lại mật khẩu", { exact: true }).fill("Second@123");
  await page.getByRole("button", { name: "Xác nhận đăng ký", exact: true }).click();
  await page.waitForURL(base + "/dashboard");
  await page.locator('header a[href="/profile"]').click();
  await page.waitForURL(base + "/profile");
  await visible(page.getByLabel("Tên hiển thị", { exact: true }));
  assert.equal(await page.getByLabel("Tên hiển thị", { exact: true }).inputValue(), "secondauthor");
  await visible(page.locator("header").getByText("secondauthor", { exact: true }));
  assert.deepEqual(pageErrors, []);
  console.log("PASS: sidebar/home partial failure/retry, categories failure/retry, responsive desktop/mobile, detail/404/error/JSON-LD, Search filters, register/login/logout, profile clear + Header sync + switch user, password validation/change/forgot/reset, protected routes; no pageerror. API giả lập; PostgreSQL/SMTP được kiểm chứng riêng.");
} catch (error) {
  console.error(log);
  if (page) {
    console.error("UI alerts:", await page.getByRole("alert").allTextContents());
    console.error("Test API calls:", calls.map(call => `${call.method} ${call.path}`));
    console.error("Fixture profile:", users.get("author+ui@example.test")?.profile);
    await page.screenshot({ path: fileURLToPath(new URL("failure.png", evidence)), fullPage: true });
  }
  throw error;
}
finally {
  if (browser) await browser.close();
  next.kill(); if (next.exitCode === null) await new Promise(resolve => next.once("exit", resolve));
  await new Promise(resolve => backend.close(resolve));
}
