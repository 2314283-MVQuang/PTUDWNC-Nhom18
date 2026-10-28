import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

const moduleUrl = code => `data:text/javascript;base64,${Buffer.from(code).toString("base64")}`;
async function compile(path) {
  return ts.transpileModule(await readFile(new URL(path, import.meta.url), "utf8"), {
    compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 },
  }).outputText;
}
const schema = await import(moduleUrl((await compile("../lib/validations/auth.schema.ts"))
  .replace('from "zod"', `from "${import.meta.resolve("zod")}"`)));
const structured = await import(moduleUrl(await compile("../lib/structured-data.ts")));
const apiStub = moduleUrl(`
  export class ApiError extends Error { constructor(status) { super('API failed'); this.status = status; } }
  export async function apiFetch(path, options) {
    globalThis.__accountCalls.push({ path, options });
    if (globalThis.__accountFailure) throw new ApiError(globalThis.__accountFailure);
    return globalThis.__accountProfile;
  }
`);
const mode = moduleUrl(await compile("../lib/auth-mode.ts"));
const { authConfig } = await import(moduleUrl((await compile("../auth.config.ts"))
  .replace('from "@/lib/api-client"', `from "${apiStub}"`)
  .replace('from "@/lib/auth-mode"', `from "${mode}"`)));

test("mật khẩu mới chặn yếu/không khớp; đổi mật khẩu cần mật khẩu hiện tại", () => {
  const values = { currentPassword: "Old@1234", newPassword: "New@1234", confirmPassword: "New@1234" };
  assert.equal(schema.changePasswordSchema.safeParse(values).success, true);
  for (const patch of [{ currentPassword: "" }, { newPassword: "short" }, { newPassword: "lowercase123!" },
    { newPassword: "NoNumber!" }, { newPassword: "NoSpecial123" }, { confirmPassword: "different" }]) {
    assert.equal(schema.changePasswordSchema.safeParse({ ...values, ...patch }).success, false);
  }
});

test("reset cần email/token, giữ nguyên token có + / =; quên mật khẩu kiểm tra email", () => {
  const values = { email: "author+test@example.test", token: "a+b/c==", newPassword: "New@1234", confirmPassword: "New@1234" };
  assert.equal(schema.resetPasswordSchema.parse(values).token, values.token);
  assert.equal(schema.resetPasswordSchema.safeParse({ ...values, token: "" }).success, false);
  assert.equal(schema.forgotPasswordSchema.safeParse({ email: "invalid" }).success, false);
  assert.equal(schema.forgotPasswordSchema.parse({ email: " author@example.test " }).email, "author@example.test");
});

test("hồ sơ cho phép xóa trường tùy chọn và khớp giới hạn backend", () => {
  assert.equal(schema.updateProfileSchema.safeParse({ displayName: "Tên", avatarUrl: "", bio: "" }).success, true);
  assert.equal(schema.updateProfileSchema.safeParse({ displayName: "Tên", avatarUrl: "/uploads/avatar.png", bio: "a".repeat(1000) }).success, true);
  for (const patch of [{ displayName: " " }, { displayName: "a".repeat(101) }, { bio: "a".repeat(1001) }, { avatarUrl: "javascript:alert(1)" }]) {
    assert.equal(schema.updateProfileSchema.safeParse({ displayName: "Tên", ...patch }).success, false);
  }
});

test("JSON-LD giữ dữ liệu nhưng không cho nội dung đóng thẻ script", () => {
  const value = { name: '</script><script>globalThis.attacked=true</script>', description: "Phở < bò & rau" };
  const encoded = structured.serializeJsonLd(value);
  assert.equal(encoded.includes("<"), false);
  assert.deepEqual(JSON.parse(encoded), value);
});

test("session update chỉ nhận hồ sơ từ backend, bỏ qua tên/role/token giả do client gửi", async () => {
  globalThis.__accountCalls = []; globalThis.__accountFailure = 0;
  globalThis.__accountProfile = { id: "author", displayName: "Tên thật", email: "author@example.test", avatarUrl: null, roles: ["Author"] };
  const token = { sub: "author", name: "Tên cũ", accessToken: "real-access", refreshToken: "server-only", accessTokenExpires: Date.now() + 60000, roles: ["Author"] };
  const updated = await authConfig.callbacks.jwt({ token, trigger: "update", session: { user: { name: "Attacker" }, roles: ["Admin"], accessToken: "fake" } });
  assert.equal(updated.name, "Tên thật");
  assert.deepEqual(updated.roles, ["Author"]);
  assert.equal(updated.accessToken, "real-access");
  assert.deepEqual(globalThis.__accountCalls.map(x => x.path), ["/auth/me"]);
  const session = await authConfig.callbacks.session({ session: { user: {} }, token: updated });
  assert.equal(session.user.name, "Tên thật");
  assert.equal(session.refreshToken, undefined);
});

test("lỗi đồng bộ tạm thời giữ phiên; backend 401 buộc xác thực lại", async () => {
  const fresh = () => ({ accessToken: "real-access", refreshToken: "server-only", accessTokenExpires: Date.now() + 60000 });
  globalThis.__accountCalls = []; globalThis.__accountFailure = 503;
  const temporary = await authConfig.callbacks.jwt({ token: fresh(), trigger: "update" });
  assert.equal(temporary.accessToken, "real-access");
  globalThis.__accountFailure = 401;
  const invalid = await authConfig.callbacks.jwt({ token: fresh(), trigger: "update" });
  assert.equal(invalid.accessToken, undefined);
  assert.equal(invalid.refreshToken, undefined);
  globalThis.__accountFailure = 0;
});
