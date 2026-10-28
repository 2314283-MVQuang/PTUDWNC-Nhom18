import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

/**
 * Tuần 8 (Quang) — hoàn thiện frontend: upload ảnh qua presigned URL, Auth nâng cao
 * (quên/đặt lại/đổi mật khẩu, xác nhận email). fetch được giả lập, không cần backend thật.
 */
const cache = new Map();
async function loadTs(relativePath) {
  if (cache.has(relativePath)) return cache.get(relativePath);
  const source = await readFile(new URL(relativePath, import.meta.url), "utf8");
  let code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
  for (const [, dep] of code.matchAll(/from "@\/lib\/([\w/-]+)"/g)) {
    const url = (await loadTs(`../lib/${dep}.ts`)).url;
    code = code.replaceAll(`from "@/lib/${dep}"`, `from "${url}"`);
  }
  code = code.replaceAll('from "zod"', `from "${import.meta.resolve("zod")}"`);
  const url = `data:text/javascript;base64,${Buffer.from(code).toString("base64")}`;
  const entry = { url, module: await import(url) };
  cache.set(relativePath, entry);
  return entry;
}

const { module: images } = await loadTs("../lib/recipe-images.ts");
const { module: schemas } = await loadTs("../lib/validations/auth.schema.ts");

const json = (status, body) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
const png = new File([new Uint8Array([137, 80, 78, 71])], "pho-bo.png", { type: "image/png" });

test("kiểm tra file ảnh trước khi upload khớp giới hạn backend", () => {
  assert.equal(images.validateImageFile({ name: "a.png", type: "image/png", size: 100 }), null);
  assert.equal(images.validateImageFile({ name: "a.JPG", type: "image/jpeg", size: 100 }), null);
  assert.match(images.validateImageFile({ name: "a.avif", type: "image/avif", size: 100 }), /JPEG/);
  assert.match(images.validateImageFile({ name: "virus.png.exe", type: "image/png", size: 100 }), /JPEG/);
  assert.match(images.validateImageFile({ name: "a.png", type: "image/png", size: 0 }), /rỗng/);
  assert.match(images.validateImageFile({ name: "a.png", type: "image/png", size: 10 * 1024 * 1024 + 1 }), /10MB/);
});

test("upload ảnh: xin presigned URL → PUT lên MinIO → gắn ảnh vào công thức", async t => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (url, init = {}) => {
    calls.push({ url: String(url), method: init.method, headers: init.headers, body: init.body });
    if (String(url).endsWith("/files/presigned-url"))
      return json(200, { data: { fileId: "f1", uploadUrl: "http://localhost:9000/recipe-images/uploads/x.png?sig=1",
        publicUrl: "http://localhost:9000/recipe-images/uploads/x.png", storageKey: "uploads/x.png",
        fileName: "pho-bo.png", contentType: "image/png", size: 4, expiresAt: "2026-10-21T00:00:00Z" } });
    if (String(url).startsWith("http://localhost:9000/")) return new Response(null, { status: 200 });
    return json(201, { data: { id: "img1", originalUrl: "http://localhost:9000/recipe-images/uploads/x.png", isPrimary: true } });
  });

  const image = await images.uploadRecipeImage("r1", png, { token: "jwt" });

  assert.equal(calls.length, 3);
  assert.match(calls[0].url, /\/files\/presigned-url$/);
  assert.deepEqual(JSON.parse(calls[0].body), { fileName: "pho-bo.png", contentType: "image/png", size: 4 });
  assert.equal(calls[0].headers.Authorization, "Bearer jwt");
  assert.equal(calls[1].method, "PUT");
  assert.equal(calls[1].headers["Content-Type"], "image/png");
  assert.equal(calls[1].body, png);
  assert.equal(calls[1].headers.Authorization, undefined, "không gửi JWT sang MinIO");
  assert.match(calls[2].url, /\/recipes\/r1\/images$/);
  assert.equal(calls[2].method, "POST");
  assert.equal(JSON.parse(calls[2].body).imageUrl, "http://localhost:9000/recipe-images/uploads/x.png");
  assert.equal(image.id, "img1");
});

test("upload ảnh: MinIO từ chối thì dừng, không gắn ảnh hỏng vào công thức", async t => {
  let attached = false;
  t.mock.method(globalThis, "fetch", async (url) => {
    if (String(url).endsWith("/files/presigned-url"))
      return json(200, { data: { uploadUrl: "http://localhost:9000/u", publicUrl: "http://localhost:9000/p", contentType: "image/png" } });
    if (String(url).endsWith("/images")) attached = true;
    return new Response("<Error><Code>AccessDenied</Code></Error>", { status: 403 });
  });
  await assert.rejects(
    images.uploadRecipeImage("r1", png),
    error => error instanceof images.StorageUploadError && /HTTP 403, AccessDenied/.test(error.message),
  );
  assert.equal(attached, false);
});

test("upload ảnh: file sai loại bị chặn trước khi gọi API", async t => {
  const fetchMock = t.mock.method(globalThis, "fetch", async () => json(200, {}));
  const bad = new File(["x"], "note.txt", { type: "text/plain" });
  await assert.rejects(images.uploadRecipeImage("r1", bad), images.StorageUploadError);
  assert.equal(fetchMock.mock.callCount(), 0);
});

test("token trong link email: dấu + bị đọc thành dấu cách được sửa lại", () => {
  assert.equal(schemas.normalizeAuthToken(" CfDJ8 abc/def== "), "CfDJ8+abc/def==");
});

test("schema đặt lại / đổi mật khẩu dùng đúng chính sách mật khẩu backend", () => {
  const ok = { email: "a@b.vn", token: "t", newPassword: "Quang@123", confirmPassword: "Quang@123" };
  assert.equal(schemas.resetPasswordSchema.safeParse(ok).success, true);
  for (const newPassword of ["short1!", "quang@123", "Quang@abc", "Quang1234"])
    assert.equal(schemas.resetPasswordSchema.safeParse({ ...ok, newPassword, confirmPassword: newPassword }).success, false, newPassword);
  assert.equal(schemas.resetPasswordSchema.safeParse({ ...ok, confirmPassword: "Khac@123" }).success, false);

  const change = { currentPassword: "Old@1234", newPassword: "New@1234", confirmPassword: "New@1234" };
  assert.equal(schemas.changePasswordSchema.safeParse(change).success, true);
  assert.equal(schemas.changePasswordSchema.safeParse({ ...change, newPassword: "Old@1234", confirmPassword: "Old@1234" }).success, false);
  assert.equal(schemas.forgotPasswordSchema.safeParse({ email: "không-phải-email" }).success, false);
  assert.equal(schemas.confirmEmailSchema.safeParse({ userId: "u1", token: "" }).success, false);
});
