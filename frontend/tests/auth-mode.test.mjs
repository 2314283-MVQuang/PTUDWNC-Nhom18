import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

// Dùng TypeScript đã có trong dự án, chạy được cả Node 20 mà không thêm test framework.
async function loadTs(relativePath) {
  const source = await readFile(new URL(relativePath, import.meta.url), "utf8");
  const result = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext } });
  const code = result.outputText.replace('from "zod"', `from "${import.meta.resolve("zod")}"`);
  return import(`data:text/javascript;base64,${Buffer.from(code).toString("base64")}`);
}

const mode = await loadTs("../lib/auth-mode.ts");
const schemas = await loadTs("../lib/validations/auth.schema.ts");

test("demo chỉ bật ở Development và khi cờ được bật rõ ràng", () => {
  const previousNode = process.env.NODE_ENV;
  const previousDemo = process.env.AUTH_DEMO_ENABLED;
  try {
    for (const [environment, flag, expected] of [
      ["development", "true", true], ["development", "false", false],
      ["production", "true", false], ["test", "true", false],
    ]) {
      process.env.NODE_ENV = environment;
      process.env.AUTH_DEMO_ENABLED = flag;
      assert.equal(mode.isDemoAuthEnabled(), expected);
    }
  } finally {
    if (previousNode === undefined) delete process.env.NODE_ENV;
    else process.env.NODE_ENV = previousNode;
    if (previousDemo === undefined) delete process.env.AUTH_DEMO_ENABLED;
    else process.env.AUTH_DEMO_ENABLED = previousDemo;
  }
});

test("nhận diện token demo để không gửi nó sang API", () => {
  assert.equal(mode.isLocalAccessToken("local-token-author"), true);
  assert.equal(mode.isLocalAccessToken("eyJhbGciOiJIUzI1NiJ9.test.signature"), false);
  assert.equal(mode.isLocalAccessToken(undefined), false);
});

test("callbackUrl chỉ cho điều hướng nội bộ", () => {
  assert.equal(mode.safeCallbackUrl("/dashboard/recipes"), "/dashboard/recipes");
  for (const value of [null, "https://evil.test", "//evil.test", "/\\evil.test", "/\r\nevil"])
    assert.equal(mode.safeCallbackUrl(value), "/dashboard");
});

test("schema Production yêu cầu email và mật khẩu theo backend", () => {
  const valid = { account: "author", fullName: "Tác giả", email: "author@example.test", password: "Author@123", confirmPassword: "Author@123" };
  assert.equal(schemas.registerSchema.safeParse(valid).success, true);
  for (const patch of [{ email: "invalid" }, { password: "123", confirmPassword: "123" }, { confirmPassword: "different" }, { account: "a/b" }])
    assert.equal(schemas.registerSchema.safeParse({ ...valid, ...patch }).success, false);
});

test("schema demo riêng giữ tài khoản và mật khẩu mẫu đơn giản", () => {
  const input = { account: "2312", password: "123", confirmPassword: "123" };
  assert.equal(schemas.demoRegisterSchema.safeParse(input).success, true);
  assert.equal(schemas.registerSchema.safeParse(input).success, false);
});
