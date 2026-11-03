// Chạy giao diện thật với API demo tuần 8 thật; không dùng API mock và không sửa .env.local.
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { createServer } from "node:net";
import { readFile, mkdir, cp } from "node:fs/promises";
import { basename, dirname } from "node:path";
import { pathToFileURL, fileURLToPath } from "node:url";

const manifestPath = process.argv[2];
assert.ok(
  manifestPath &&
    /^culinary-week8-demo-[a-f0-9]+$/.test(basename(dirname(manifestPath))),
  "Dùng đường dẫn demo.json do script tuần 8 tạo trong TEMP.",
);
assert.ok(
  process.env.CULINARY_PLAYWRIGHT_MODULE_PATH,
  "Đặt CULINARY_PLAYWRIGHT_MODULE_PATH tới playwright-core/index.mjs.",
);
const info = JSON.parse(await readFile(manifestPath, "utf8"));
assert.equal(new URL(info.apiBaseUrl).hostname, "127.0.0.1");
const categories = await (await fetch(info.apiBaseUrl + "/categories")).json();
assert.ok(
  categories.data.some((category) => category.id === info.categoryId),
  "API demo không còn hoạt động.",
);
const probe = createServer();
await new Promise((resolve, reject) => {
  probe.once("error", reject);
  probe.listen(3000, "127.0.0.1", resolve);
});
await new Promise((resolve) => probe.close(resolve));
const { chromium } = await import(
  pathToFileURL(process.env.CULINARY_PLAYWRIGHT_MODULE_PATH).href
);
const base = "http://localhost:3000";
const frontendRoot = new URL("..", import.meta.url);
const evidence = new URL(
  "../../spec/minh_chung_lab8_2026_11_03/",
  import.meta.url,
);
await mkdir(evidence, { recursive: true });
const production = process.env.CULINARY_WEEK8_UI_MODE === "production";
if (production) {
  await cp(
    new URL(".next/static", frontendRoot),
    new URL(".next/standalone/.next/static", frontendRoot),
    { recursive: true },
  );
  await cp(
    new URL("public", frontendRoot),
    new URL(".next/standalone/public", frontendRoot),
    { recursive: true },
  );
}
const serverArguments = production
  ? [".next/standalone/server.js"]
  : [
      "node_modules/next/dist/bin/next",
      "dev",
      "--hostname",
      "127.0.0.1",
      "--port",
      "3000",
    ];
const next = spawn(process.execPath, serverArguments, {
  cwd: frontendRoot,
  windowsHide: true,
  stdio: "ignore",
  env: {
    ...process.env,
    NODE_ENV: production ? "production" : "development",
    HOSTNAME: "127.0.0.1",
    PORT: "3000",
    AUTH_DEMO_ENABLED: "false",
    AUTH_SECRET: randomBytes(32).toString("base64"),
    AUTH_GOOGLE_ID: "",
    AUTH_GOOGLE_SECRET: "",
    AUTH_URL: base,
    NEXTAUTH_URL: base,
    AUTH_TRUST_HOST: "true",
    NEXT_PUBLIC_API_BASE_URL: info.apiBaseUrl,
    API_INTERNAL_BASE_URL: info.apiBaseUrl,
  },
});
let browser;
let page;
let guestPage;
try {
  let ready = false;
  for (let attempt = 0; attempt < 120; attempt++) {
    if (next.exitCode !== null)
      throw new Error("Next.js không khởi động được.");
    try {
      if ((await fetch(base + "/api/auth/providers")).ok) {
        ready = true;
        break;
      }
    } catch {
      /* chờ server */
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  assert.ok(ready, "Next.js chưa sẵn sàng.");
  browser = await chromium.launch({ channel: "msedge", headless: true });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  page = await context.newPage();
  page.setDefaultTimeout(30000);
  page.setDefaultNavigationTimeout(120000);
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("requestfailed", (request) => {
    if (request.failure()?.errorText !== "net::ERR_ABORTED")
      console.error(
        "Request failed:",
        new URL(request.url()).pathname,
        request.failure()?.errorText,
      );
  });
  const visible = (locator) => locator.waitFor({ state: "visible" });
  await page.goto(base + "/register?callbackUrl=%2Fdashboard%2Frecipes%2Fnew", {
    waitUntil: "domcontentloaded",
  });
  await page.waitForLoadState("networkidle", { timeout: 120000 });
  const accountName = "week8ui" + randomBytes(6).toString("hex");
  await page.getByLabel("Tên tài khoản", { exact: true }).fill(accountName);
  await page
    .getByLabel("Email đăng ký", { exact: true })
    .fill(accountName + "@example.test");
  await page.getByLabel("Mật khẩu", { exact: true }).fill(info.password);
  await page
    .getByLabel("Xác nhận lại mật khẩu", { exact: true })
    .fill(info.password);
  const registrationResponse = page.waitForResponse(
    (response) =>
      response.url() === base + "/api/auth/register" &&
      response.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Xác nhận đăng ký", exact: true })
    .click();
  assert.equal(
    (await registrationResponse).status(),
    201,
    "Route frontend đăng ký không trả 201.",
  );
  console.log("PASS: đăng ký qua frontend/backend thật.");
  await page.waitForURL("**/dashboard/recipes/new", {
    timeout: 120000,
    waitUntil: "domcontentloaded",
  });
  await visible(
    page.getByRole("heading", { name: "Tạo công thức mới", exact: true }),
  );
  const title = "Phở bò giao diện tuần 8 " + randomBytes(3).toString("hex");
  await page.getByLabel("Tên công thức", { exact: true }).fill(title);
  await page
    .getByLabel("Mô tả ngắn", { exact: true })
    .fill("Công thức kiểm thử tích hợp giao diện với PostgreSQL thật.");
  await page
    .getByLabel("Danh mục", { exact: true })
    .selectOption(info.categoryId);
  await page.getByLabel("Chuẩn bị (phút)", { exact: true }).fill("20");
  await page.getByLabel("Thời gian nấu (phút)", { exact: true }).fill("60");
  await page.getByLabel("Khẩu phần", { exact: true }).fill("4");
  await page.getByLabel("Độ khó", { exact: true }).selectOption("Easy");
  await page.getByRole("button", { name: "Tiếp theo", exact: true }).click();
  await page
    .getByPlaceholder("Tên nguyên liệu", { exact: true })
    .fill("Thịt bò");
  await page.getByPlaceholder("Số lượng", { exact: true }).fill("0.5");
  await page
    .getByPlaceholder("Đơn vị (gram, ml...)", { exact: true })
    .fill("kg");
  await page.getByRole("button", { name: "Thêm", exact: true }).click();
  await page.getByRole("button", { name: "Tiếp theo", exact: true }).click();
  await page
    .getByPlaceholder("Tiêu đề bước", { exact: true })
    .fill("Ninh nước dùng");
  await page
    .getByPlaceholder("Mô tả chi tiết", { exact: true })
    .fill("Ninh xương bò với lửa nhỏ rồi thêm gia vị.");
  await page.getByRole("button", { name: "Thêm bước", exact: true }).click();
  await page.getByRole("button", { name: "Tiếp theo", exact: true }).click();
  const createResponse = page.waitForResponse(
    (response) =>
      response.url() === info.apiBaseUrl + "/recipes" &&
      response.request().method() === "POST",
  );
  const png = Buffer.from(
    await page.evaluate(() => {
      const canvas = document.createElement("canvas");
      canvas.width = 640;
      canvas.height = 400;
      const context = canvas.getContext("2d");
      context.fillStyle = "#fff4e6";
      context.fillRect(0, 0, 640, 400);
      context.fillStyle = "#ed6a32";
      context.beginPath();
      context.arc(320, 200, 140, 0, Math.PI * 2);
      context.fill();
      context.fillStyle = "#ffffff";
      context.font = "bold 32px sans-serif";
      context.textAlign = "center";
      context.fillText("Culinary Blog", 320, 195);
      context.font = "20px sans-serif";
      context.fillText("Lab 8 - upload", 320, 230);
      return canvas.toDataURL("image/png").split(",")[1];
    }),
    "base64",
  );
  await page
    .getByLabel("Ảnh món ăn (tùy chọn)", { exact: true })
    .setInputFiles({ name: "recipe.png", mimeType: "image/png", buffer: png });
  const coverResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith("/images") &&
      response.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Lưu công thức", exact: true })
    .click();
  const createdResponse = await createResponse;
  assert.equal(createdResponse.status(), 201);
  const draft = (await createdResponse.json()).data;
  assert.equal((await coverResponse).status(), 201);
  await page.waitForURL("**/dashboard/recipes/*/edit", {
    timeout: 120000,
    waitUntil: "domcontentloaded",
  });
  await visible(
    page.getByRole("heading", { name: "Chỉnh sửa công thức", exact: true }),
  );
  await page.screenshot({
    path: fileURLToPath(new URL("recipe-draft.png", evidence)),
    fullPage: true,
  });

  const invalidImageResponse = page.waitForResponse(response => response.url().endsWith("/images") && response.request().method() === "POST");
  await page.getByLabel("Chọn ảnh công thức", {exact:true}).setInputFiles({name:"fake.png",mimeType:"image/png",buffer:Buffer.from("not-an-image")});
  assert.equal((await invalidImageResponse).status(),400);
  await visible(page.getByRole("alert").filter({hasText:"Định dạng"}));

  await page
    .getByLabel("Mô tả ảnh (tùy chọn)", { exact: true })
    .fill("Ảnh minh họa bổ sung");
  const extraImageResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith("/images") &&
      response.request().method() === "POST",
  );
  await page
    .getByLabel("Chọn ảnh công thức", { exact: true })
    .setInputFiles({ name: "second.png", mimeType: "image/png", buffer: png });
  assert.equal((await extraImageResponse).status(), 201);
  await visible(
    page.getByRole("button", { name: "Đặt làm đại diện", exact: true }),
  );
  const primaryResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith("/primary") &&
      response.request().method() === "PATCH",
  );
  await page
    .getByRole("button", { name: "Đặt làm đại diện", exact: true })
    .click();
  assert.equal((await primaryResponse).status(), 200);
  await page.screenshot({
    path: fileURLToPath(new URL("recipe-images.png", evidence)),
    fullPage: true,
  });

  const updateResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith("/recipes/" + draft.id) &&
      response.request().method() === "PUT",
  );
  await page
    .getByLabel("Tên công thức", { exact: true })
    .fill(title + " đã sửa");
  await page.getByRole("button", { name: "Lưu thay đổi", exact: true }).click();
  const updatedResponse = await updateResponse;
  assert.equal(updatedResponse.status(), 200);
  const updated = (await updatedResponse.json()).data;
  await page.waitForURL(
    (url) => url.pathname === `/dashboard/recipes/${updated.slug}/edit`,
    { timeout: 30000 },
  );
  const publishResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith("/publish") &&
      response.request().method() === "PATCH",
  );
  await page.getByRole("button", { name: "Xuất bản", exact: true }).click();
  const publishedResponse = await publishResponse;
  assert.equal(publishedResponse.status(), 200);
  const published = (await publishedResponse.json()).data;
  await visible(page.getByText("Đã đăng", { exact: true }));
  await page.screenshot({
    path: fileURLToPath(new URL("recipe-published.png", evidence)),
    fullPage: true,
  });

  const guest = await browser.newContext();
  guestPage = await guest.newPage();
  guestPage.on("pageerror", (error) => errors.push(error.message));
  await guestPage.goto(base + "/recipes/" + published.slug, {
    waitUntil: "domcontentloaded",
    timeout: 120000,
  });
  await visible(
    guestPage.getByRole("heading", { name: published.title, exact: true }),
  );
  await guestPage.screenshot({
    path: fileURLToPath(new URL("recipe-public.png", evidence)),
    fullPage: true,
  });
  assert.equal(
    await guestPage.getByRole("heading", { name: /Dinh dưỡng/ }).count(),
    0,
  );
  await guestPage.waitForFunction(() =>
    [...document.images].some(
      (image) =>
        image.currentSrc.includes("/recipe-images/") && image.naturalWidth > 0,
    ),
  );
  await guestPage.setViewportSize({ width: 390, height: 844 });
  assert.ok(
    await guestPage.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
  );
  await guestPage.screenshot({
    path: fileURLToPath(new URL("recipe-mobile.png", evidence)),
    fullPage: true,
  });
  await guestPage.setViewportSize({ width: 1440, height: 1000 });
  const archiveResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith("/archive") &&
      response.request().method() === "PATCH",
  );
  await page.getByRole("button", { name: "Lưu trữ", exact: true }).click();
  assert.equal((await archiveResponse).status(), 200);
  await visible(page.getByText("Đã lưu trữ", { exact: true }));
  await guestPage.reload({ waitUntil: "domcontentloaded" });
  await visible(
    guestPage.getByRole("heading", {
      name: "Không tìm thấy công thức",
      exact: true,
    }),
  );
  const restoreResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith("/unarchive") &&
      response.request().method() === "PATCH",
  );
  await page.getByRole("button", { name: "Bỏ lưu trữ", exact: true }).click();
  assert.equal((await restoreResponse).status(), 200);
  await visible(page.getByText("Đã đăng", { exact: true }));
  await guestPage.reload({ waitUntil: "domcontentloaded" });
  await visible(
    guestPage.getByRole("heading", { name: published.title, exact: true }),
  );
  page.once("dialog", (dialog) => dialog.accept());
  const deleteResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith("/recipes/" + draft.id) &&
      response.request().method() === "DELETE",
  );
  await page.getByRole("button", { name: "Xoá", exact: true }).click();
  assert.equal((await deleteResponse).status(), 204);
  await page.waitForURL("**/dashboard/recipes");
  assert.equal(
    (await fetch(info.apiBaseUrl + "/recipes/" + published.slug)).status,
    404,
  );
  console.log(
    "PASS: tạo → sửa → publish → xem công khai → xóa; API đã trả 404.",
  );
  await guestPage.reload({ waitUntil: "domcontentloaded", timeout: 120000 });
  try {
    await guestPage
      .getByRole("heading", { name: "Không tìm thấy công thức", exact: true })
      .waitFor({ state: "visible", timeout: 10000 });
  } catch {
    await guestPage.screenshot({
      path: fileURLToPath(new URL("recipe-after-delete.png", evidence)),
      fullPage: true,
    });
    const headings = await guestPage.getByRole("heading").allTextContents();
    console.error("Tiêu đề trang công khai sau DELETE:", headings);
    assert.fail(
      "API trả 404 sau DELETE nhưng trang công khai chưa hiển thị trạng thái không tìm thấy.",
    );
  }
  assert.equal(
    await guestPage
      .getByRole("heading", { name: published.title, exact: true })
      .count(),
    0,
  );
  await guestPage.screenshot({
    path: fileURLToPath(new URL("recipe-after-delete-fixed.png", evidence)),
    fullPage: true,
  });
  await page.goto(base + "/dashboard/users", { waitUntil: "domcontentloaded" });
  await page.waitForURL((url) => url.pathname === "/dashboard");
  assert.equal((await fetch(info.apiBaseUrl + "/users")).status, 401);
  await page.getByRole("button", { name: "Đăng xuất", exact: true }).click();
  await page.waitForURL((url) => url.pathname === "/");
  await page.goto(base + "/login?callbackUrl=%2Fdashboard%2Fusers", {
    waitUntil: "domcontentloaded",
  });
  await page.waitForLoadState("networkidle");
  await page.getByLabel("Email", { exact: true }).fill(info.adminEmail);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(info.password);
  await page
    .locator("main form")
    .getByRole("button", { name: "Đăng nhập", exact: true })
    .click();
  await page.waitForURL("**/dashboard/users");
  await visible(page.getByRole("heading", { name: "Người dùng", exact: true }));
  await visible(page.getByRole("link", { name: "Trang 2", exact: true }));
  await page.getByRole("link", { name: "Trang 2", exact: true }).click();
  await page.waitForURL((url) => url.searchParams.get("page") === "2");
  await visible(page.locator("tbody tr").first());
  const adminFilterResponse = page.waitForResponse(response => new URL(response.url()).pathname === "/api/v1/users" && new URL(response.url()).searchParams.get("role") === "Admin");
  await page.getByLabel("Vai trò", { exact: true }).selectOption("Admin");
  assert.equal((await adminFilterResponse).status(),200);
  await page.waitForURL(url => url.searchParams.get("role") === "Admin" && url.searchParams.get("page") === "1");
  await page.waitForFunction(() => document.querySelectorAll("tbody tr").length === 1);
  await visible(
    page.locator("tbody").getByText(info.adminEmail, { exact: true }),
  );
  assert.equal(await page.locator("tbody tr").count(), 1);
  await page.screenshot({
    path: fileURLToPath(new URL("admin-users.png", evidence)),
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  assert.ok(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
  );
  await page.screenshot({
    path: fileURLToPath(new URL("admin-users-mobile.png", evidence)),
    fullPage: true,
  });
  await page.getByLabel("Vai trò", { exact: true }).selectOption("");
  await page.waitForURL((url) => !url.searchParams.get("role"));
  await page
    .getByLabel("Tìm người dùng", { exact: true })
    .fill(info.ownerEmail);
  await page
    .locator("main form")
    .getByRole("button", { name: "Tìm kiếm", exact: true })
    .click();
  await visible(
    page.locator("tbody").getByText(info.ownerEmail, { exact: true }),
  );
  assert.equal(await page.locator("tbody tr").count(), 1);
  assert.deepEqual(errors, [], "Giao diện phát sinh lỗi JavaScript.");
  console.log(
    `PASS (${production ? "Production" : "Development"}): upload, ảnh đại diện, tạo/sửa/Publish/Archive/khôi phục/xóa, responsive, phân quyền và danh sách người dùng.`,
  );
} catch (error) {
  if (page) {
    console.error("Trang dừng:", new URL(page.url()).pathname);
    console.error(
      (
        await page
          .locator("body")
          .innerText()
          .catch(() => "")
      ).slice(-1800),
    );
    console.error(
      "HTML validation:",
      await page
        .locator("input:invalid")
        .evaluateAll((inputs) =>
          inputs.map((input) => ({
            name: input.name,
            reason: input.validationMessage,
          })),
        ),
    );
  }
  throw error;
} finally {
  if (browser) await browser.close();
  if (next.exitCode === null) {
    if (process.platform === "win32") {
      // Next dev tạo tiến trình con; chỉ dừng cây tiến trình do chính script này vừa khởi động.
      await new Promise((resolve) => {
        const stop = spawn("taskkill", ["/pid", String(next.pid), "/t", "/f"], {
          windowsHide: true,
          stdio: "ignore",
        });
        stop.once("close", resolve);
        stop.once("error", () => {
          next.kill();
          resolve();
        });
      });
    } else next.kill();
  }
}
