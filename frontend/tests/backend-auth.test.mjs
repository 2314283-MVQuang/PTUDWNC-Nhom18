import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

/**
 * Tuần 7 (Quang) — kiểm thử phần nối frontend đăng ký/đăng nhập với backend .NET.
 * Không cần backend chạy thật: `globalThis.fetch` được thay bằng hàm giả trả đúng format
 * backend (ApiEnvelope `{ data }` khi thành công, RFC 7807 khi lỗi).
 */
const cache = new Map();

// Transpile file TS rồi import qua data: URL; import "@/lib/..." được nối tới file tương ứng.
async function loadTs(relativePath) {
  if (cache.has(relativePath)) return cache.get(relativePath);
  const source = await readFile(new URL(relativePath, import.meta.url), "utf8");
  // target ES2022: TypeScript 5.x mặc định ES5 làm `class ApiError extends Error` mất instanceof.
  let code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
  for (const [, dep] of code.matchAll(/from "@\/lib\/([\w-]+)"/g)) {
    const url = (await loadTs(`../lib/${dep}.ts`)).url;
    code = code.replaceAll(`from "@/lib/${dep}"`, `from "${url}"`);
  }
  const url = `data:text/javascript;base64,${Buffer.from(code).toString("base64")}`;
  const entry = { url, module: await import(url) };
  cache.set(relativePath, entry);
  return entry;
}

const { module: auth } = await loadTs("../lib/backend-auth.ts");
const { module: errors } = await loadTs("../lib/auth-errors.ts");

const profile = { id: "u-1", email: "quang@dlu.edu.vn", displayName: "Mai Văn Quang", avatarUrl: null, bio: null, roles: ["Author"] };
const tokens = { accessToken: "eyJ.real.jwt", refreshToken: "refresh-abc", expiresIn: 900, user: profile };

/** Thay fetch bằng hàm giả; trả về danh sách request đã gửi để kiểm tra. */
function mockFetch(handler) {
  const calls = [];
  const original = globalThis.fetch;
  globalThis.fetch = async (url, init = {}) => {
    calls.push({ url: String(url), method: init.method, headers: init.headers ?? {}, body: init.body ? JSON.parse(init.body) : undefined });
    return handler(String(url), init);
  };
  return { calls, restore: () => { globalThis.fetch = original; } };
}

const json = (status, body) => new Response(body === undefined ? null : JSON.stringify(body), {
  status, headers: { "Content-Type": "application/json" },
});
const problem = (status, type, detail, extra = {}) => json(status, { type, title: type, status, detail, ...extra });

test("đăng nhập gọi đúng 1 request POST /auth/login và dùng JWT thật từ backend", async () => {
  const { calls, restore } = mockFetch(() => json(200, { data: tokens }));
  try {
    const before = Date.now();
    const user = await auth.loginWithBackend("quang@dlu.edu.vn", "Quang@123");
    assert.equal(calls.length, 1, "không gọi thêm /auth/me vì response login đã có user");
    assert.match(calls[0].url, /\/auth\/login$/);
    assert.equal(calls[0].method, "POST");
    assert.deepEqual(calls[0].body, { email: "quang@dlu.edu.vn", password: "Quang@123" });
    assert.equal(user.accessToken, "eyJ.real.jwt");
    assert.ok(!user.accessToken.startsWith("local-token-"), "không còn token giả");
    assert.equal(user.refreshToken, "refresh-abc");
    assert.equal(user.id, "u-1");
    assert.equal(user.name, "Mai Văn Quang");
    assert.deepEqual(user.roles, ["Author"]);
    assert.ok(user.accessTokenExpires >= before + 900_000);
  } finally { restore(); }
});

test("đăng nhập thất bại trả mã lỗi đúng theo status backend", async () => {
  for (const [status, expected] of [
    [401, "invalid_credentials"], [423, "account_locked"], [429, "rate_limited"], [500, "backend_unavailable"],
  ]) {
    const { restore } = mockFetch(() => problem(status, "X", "lỗi"));
    try {
      await assert.rejects(auth.loginWithBackend("a@b.c", "x"), (err) => err.code === expected);
    } finally { restore(); }
  }
});

test("backend không chạy (fetch lỗi mạng) → backend_unavailable chứ không phải sai mật khẩu", async () => {
  const { restore } = mockFetch(() => { throw new TypeError("fetch failed"); });
  try {
    await assert.rejects(auth.loginWithBackend("a@b.c", "x"), (err) => err.code === "backend_unavailable");
  } finally { restore(); }
});

test("đăng ký gửi đúng body RegisterCommand tới POST /auth/register", async () => {
  const { calls, restore } = mockFetch(() => json(201, { data: tokens }));
  try {
    const input = { fullName: "Mai Văn Quang", email: "quang@dlu.edu.vn", userName: "quang", password: "Quang@123" };
    const result = await auth.registerWithBackend(input);
    assert.match(calls[0].url, /\/auth\/register$/);
    assert.equal(calls[0].method, "POST");
    assert.deepEqual(calls[0].body, input);
    assert.equal(result.user.email, "quang@dlu.edu.vn");
  } finally { restore(); }
});

test("lỗi đăng ký được đổi thành thông báo cụ thể", async () => {
  const cases = [
    [problem(409, "CONFLICT", "Email đã được sử dụng."), 409, "Email đã được sử dụng."],
    [problem(422, "VALIDATION_ERROR", "Một hoặc nhiều trường dữ liệu không hợp lệ.",
      { errors: { Password: ["Mật khẩu phải có ít nhất 1 chữ hoa."] } }), 422, "Mật khẩu phải có ít nhất 1 chữ hoa."],
    [problem(429, "RATE_LIMIT_EXCEEDED", "Too many"), 429, "Bạn thao tác quá nhanh. Vui lòng đợi 1 phút rồi thử lại."],
    [problem(500, "INTERNAL_SERVER_ERROR", "boom"), 503, "Không kết nối được máy chủ. Vui lòng thử lại sau."],
  ];
  for (const [response, status, message] of cases) {
    const { restore } = mockFetch(() => response);
    try {
      const err = await auth.registerWithBackend({ fullName: "A", email: "a@b.c", password: "x" }).catch((e) => e);
      assert.deepEqual(auth.registerErrorResponse(err), { status, message });
    } finally { restore(); }
  }
  assert.equal(auth.registerErrorResponse(new TypeError("fetch failed")).status, 503);
});

test("đăng xuất thu hồi refresh token ở backend bằng Bearer token", async () => {
  const { calls, restore } = mockFetch(() => new Response(null, { status: 204 }));
  try {
    assert.equal(await auth.revokeBackendSession("eyJ.real.jwt", "refresh-abc"), true);
    assert.match(calls[0].url, /\/auth\/logout$/);
    assert.equal(calls[0].headers.Authorization, "Bearer eyJ.real.jwt");
    assert.deepEqual(calls[0].body, { refreshToken: "refresh-abc" });

    // Token demo / thiếu token: không gọi backend.
    assert.equal(await auth.revokeBackendSession("local-token-1", "local-refresh-1"), false);
    assert.equal(await auth.revokeBackendSession(undefined, undefined), false);
    assert.equal(calls.length, 1);
  } finally { restore(); }
});

test("đăng xuất không làm hỏng giao diện khi backend lỗi", async () => {
  const { restore } = mockFetch(() => problem(401, "UNAUTHORIZED", "expired"));
  try {
    assert.equal(await auth.revokeBackendSession("eyJ.old", "refresh-old"), false);
  } finally { restore(); }
});

test("thông báo lỗi đăng nhập cho từng mã", () => {
  assert.equal(errors.loginErrorMessage("invalid_credentials"), "Email hoặc mật khẩu không đúng.");
  assert.match(errors.loginErrorMessage("account_locked"), /15 phút/);
  assert.match(errors.loginErrorMessage("rate_limited"), /1 phút/);
  assert.match(errors.loginErrorMessage("backend_unavailable"), /máy chủ/);
  // Mã lạ/không có (vd. "credentials" mặc định của Auth.js) → thông báo chung, không lộ thông tin.
  assert.equal(errors.loginErrorMessage(undefined), "Email hoặc mật khẩu không đúng.");
  assert.equal(errors.loginErrorMessage("credentials"), "Email hoặc mật khẩu không đúng.");
});
