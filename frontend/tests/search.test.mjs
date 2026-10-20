import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

async function loadTs(path) {
  const source = await readFile(new URL(path, import.meta.url), "utf8");
  const { outputText } = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } });
  return import(`data:text/javascript;base64,${Buffer.from(outputText).toString("base64")}`);
}
const filters = await loadTs("../lib/search-filters.ts");
const client = await loadTs("../lib/api-client.ts");
const empty = filters.EMPTY_SEARCH_FILTERS;

test("URL round-trip giữ tiếng Việt, dấu &, số 0 và bỏ tham số ngoài phạm vi", () => {
  const draft = { ...empty, q: "phở & bò", difficulty: "Easy", minCookTime: "0", maxPrepTime: "20", minServings: "4", sort: "-createdAt" };
  const href = filters.searchHref(draft);
  const params = Object.fromEntries(new URL(href, "http://localhost").searchParams);
  assert.deepEqual(filters.readSearchFilters({ ...params, page: "999", unknown: "x" }), draft);
  assert.equal(params.minCookTime, "0");
  assert.equal(params.categoryId, undefined);
});

test("chặn khoảng ngược, số âm/lẻ/quá lớn và danh mục/độ khó không hợp lệ", () => {
  for (const patch of [
    { minPrepTime: "30", maxPrepTime: "20" }, { minCookTime: "1", maxCookTime: "0" },
    { minCookTime: "-1" }, { maxPrepTime: "1.5" }, { maxCookTime: "2147483648" },
    { minPrepTime: "1e3" }, { difficulty: "Unknown" }, { categoryId: "invalid" },
    { categoryId: "00000000-0000-0000-0000-000000000000" },
    { minServings: "0" }, { minServings: "-2" }, { minServings: "2.5" },
    { sort: "authorId" }, { sort: "-password" },
  ]) assert.ok(filters.validateSearchFilters({ ...empty, ...patch }).length > 0, JSON.stringify(patch));
  assert.deepEqual(filters.validateSearchFilters({ ...empty, minCookTime: "0", maxCookTime: "0", minPrepTime: "20", maxPrepTime: "20", minServings: "1", sort: "-cookTime" }), []);
});

test("store giữ draft độc lập, reset giữ từ khóa, sync phục hồi URL Back/Forward", () => {
  const applied = { ...empty, q: "phở", difficulty: "Easy" };
  const draft = filters.searchFilterReducer(applied, { type: "change", key: "maxCookTime", value: "30" });
  assert.equal(applied.maxCookTime, "");
  assert.equal(draft.maxCookTime, "30");
  assert.deepEqual(filters.searchFilterReducer(draft, { type: "reset" }), { ...empty, q: "phở" });
  assert.deepEqual(filters.searchFilterReducer(draft, { type: "sync", value: applied }), applied);
  assert.deepEqual(empty.q, "");
});

test("API client gửi đủ filters, unwrap data và giữ header token/If-Match", async t => {
  t.mock.method(globalThis, "fetch", async (url, options) => {
    const params = new URL(url).searchParams;
    assert.equal(params.get("minCookTime"), "0");
    assert.equal(params.get("difficulty"), "Easy");
    assert.equal(params.has("categoryId"), false);
    assert.equal(options.headers.Authorization, "Bearer jwt-test");
    assert.equal(options.headers["If-Match"], "version");
    return Response.json({ data: [{ id: "recipe" }] });
  });
  assert.deepEqual(await client.apiFetch("/recipes/search", {
    params: { q: "phở", minCookTime: 0, difficulty: "Easy", categoryId: undefined }, token: "jwt-test", ifMatch: "version",
  }), [{ id: "recipe" }]);
});

test("ProblemDetails ưu tiên lỗi field và lấy status HTTP thật", async t => {
  t.mock.method(globalThis, "fetch", async () => Response.json({ status: 200, title: "Invalid", errors: { MaxCookTime: ["Khoảng sai"], unsafe: { nested: true } } }, { status: 422 }));
  await assert.rejects(client.apiFetch("/recipes/search"), error => {
    assert.ok(error instanceof client.ApiError);
    assert.equal(error.status, 422);
    assert.equal(client.apiErrorMessage(error, "fallback"), "Khoảng sai");
    return true;
  });
});

test("lỗi HTML/JSON lạ có thông báo an toàn, không làm vỡ UI", async t => {
  for (const body of ["<html>Bad gateway</html>", "null", "[]", '{"title":42,"errors":{"field":[7]}}']) {
    t.mock.method(globalThis, "fetch", async () => new Response(body, { status: 502 }));
    await assert.rejects(client.apiFetch("/recipes/search"), error => {
      assert.ok(error instanceof client.ApiError);
      assert.equal(error.status, 502);
      assert.match(client.apiErrorMessage(error, "fallback"), /Máy chủ/);
      return true;
    });
  }
});

test("lỗi mạng có kiểu riêng; không đổi AbortError thành lỗi mạng", async t => {
  t.mock.method(globalThis, "fetch", async () => { throw new TypeError("fetch failed"); });
  await assert.rejects(client.apiFetch("/recipes/search"), client.ApiNetworkError);
  t.mock.method(globalThis, "fetch", async () => { throw new DOMException("Cancelled", "AbortError"); });
  await assert.rejects(client.apiFetch("/recipes/search"), { name: "AbortError" });
});

test("HTTP 204 trả undefined", async t => {
  t.mock.method(globalThis, "fetch", async () => new Response(null, { status: 204 }));
  assert.equal(await client.apiFetch("/recipes/id", { method: "DELETE" }), undefined);
});
