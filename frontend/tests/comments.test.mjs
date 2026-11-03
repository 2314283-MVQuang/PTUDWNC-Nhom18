import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

async function loadTs(path) {
  const source = await readFile(new URL(path, import.meta.url), "utf8");
  const { outputText } = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } });
  return import(`data:text/javascript;base64,${Buffer.from(outputText).toString("base64")}`);
}
const comments = await loadTs("../lib/comments.ts");

test("FR-CMT: bình luận rỗng hoặc chỉ có khoảng trắng bị chặn", () => {
  for (const bad of ["", "   ", "\n\t "]) assert.equal(comments.validateCommentContent(bad), "Nội dung bình luận không được để trống.");
});

test("FR-CMT: tối đa 1000 ký tự sau khi trim, khớp backend", () => {
  assert.equal(comments.COMMENT_MAX_LENGTH, 1000);
  assert.equal(comments.validateCommentContent("a".repeat(1000)), null);
  assert.equal(comments.validateCommentContent(`  ${"a".repeat(1000)}  `), null);
  assert.match(comments.validateCommentContent("a".repeat(1001)), /tối đa 1000/);
  assert.equal(comments.validateCommentContent("Ngon quá, cảm ơn bạn! 😋"), null);
});
