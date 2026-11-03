import test from "node:test";
import assert from "node:assert/strict";
import { validateGoogleProductionConfig } from "../../scripts/check-google-config.mjs";

const id = "123-test.apps.googleusercontent.com";
function configured() {
  return { services: {
    frontend: { environment: { AUTH_GOOGLE_ID: id, AUTH_GOOGLE_SECRET: "test-only-secret",
      AUTH_URL: "https://blog.example.test", NEXTAUTH_URL: "https://blog.example.test" } },
    api: { environment: { Google__ClientId: id } },
  } };
}
test("website chung có HTTPS, callback và cùng Client ID", () => {
  assert.deepEqual(validateGoogleProductionConfig(configured()), {
    errors: [], callbackUrl: "https://blog.example.test/api/auth/callback/google",
  });
});
test("từ chối thiếu khóa/sai audience và không đưa giá trị secret vào thông báo", () => {
  const config = configured();
  config.services.api.environment.Google__ClientId = "other-client";
  config.services.frontend.environment.AUTH_GOOGLE_SECRET = "";
  const result = validateGoogleProductionConfig(config);
  assert.ok(result.errors.some(value => value.includes("SECRET")));
  assert.ok(result.errors.some(value => value.includes("không khớp")));
  assert.ok(!JSON.stringify(result).includes("other-client"));
});
test("từ chối URL thử nghiệm, HTTP, query và cấu hình lộ secret phía client", () => {
  for (const url of ["http://blog.example.test", "https://localhost", "https://blog.example.test/path", "https://blog.example.test?token=x"]) {
    const config = configured(); config.services.frontend.environment.AUTH_URL = url;
    assert.ok(validateGoogleProductionConfig(config).errors.some(value => value.includes("HTTPS")));
  }
  const config = configured();
  config.services.frontend.environment.NEXT_PUBLIC_GOOGLE_SECRET = "test-only-secret";
  assert.ok(validateGoogleProductionConfig(config).errors.some(value => value.includes("NEXT_PUBLIC")));
  assert.ok(!JSON.stringify(validateGoogleProductionConfig(config)).includes("test-only-secret"));
});
