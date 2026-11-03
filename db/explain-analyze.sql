-- =============================================================================
--  Chương 3 (3.5–3.6, "measure before optimize") — đo các câu truy vấn chính bằng EXPLAIN ANALYZE.
--
--  Chạy:  powershell -ExecutionPolicy Bypass -File .\explain-analyze.ps1
--  (hoặc: psql -U culinary -d culinaryblog -f db/explain-analyze.sql)
--
--  AN TOÀN VỚI DỮ LIỆU THẬT: mọi thứ nằm trong 1 transaction và ROLLBACK ở cuối — 20 000 công
--  thức giả và việc xoá/tạo lại index đều biến mất. Trong lúc chạy (vài giây) bảng Recipes bị
--  khoá, request ghi vào công thức sẽ phải chờ.
--
--  Cần sẵn ít nhất 1 danh mục và 1 người dùng (tạo bằng web hoặc test-e2e-nguoi-dung-that.ps1).
-- =============================================================================
\set ON_ERROR_STOP on
\pset pager off
\timing off
BEGIN;

-- ---------------------------------------------------------------------------
-- 0. Dữ liệu đo: 20 000 công thức (90% Published), chia đều các danh mục đang có.
-- ---------------------------------------------------------------------------
WITH cats AS (SELECT "Id", row_number() OVER (ORDER BY "Id") - 1 AS n FROM "Categories" WHERE NOT "IsDeleted"),
     ncat AS (SELECT count(*) AS c FROM cats),
     author AS (SELECT "Id" FROM "AspNetUsers" ORDER BY "CreatedAt" LIMIT 1)
INSERT INTO "Recipes" ("Title", "Slug", "Description", "PrepTime", "CookTime", "Servings",
                       "Difficulty", "Status", "CategoryId", "AuthorId", "PublishedAt", "CreatedAt")
SELECT (ARRAY['Phở bò', 'Bún chả', 'Cơm tấm', 'Bánh xèo', 'Gỏi cuốn', 'Canh chua', 'Cá kho tộ', 'Chè đậu xanh'])[1 + g % 8]
           || ' kiểu ' || g,
       'bench-' || g,
       'Công thức đo hiệu năng số ' || g || ', nước dùng thơm, dễ nấu tại nhà.',
       5 + g % 55, 10 + g % 170, 1 + g % 6, 1 + g % 4,
       CASE WHEN g % 10 = 0 THEN 0 ELSE 1 END,
       cats."Id", author."Id",
       CASE WHEN g % 10 = 0 THEN NULL ELSE now() - make_interval(mins => g) END,
       now() - make_interval(mins => g)
FROM generate_series(1, 20000) AS g
CROSS JOIN ncat
CROSS JOIN author
JOIN cats ON cats.n = g % ncat.c;

-- Một ít công thức đã xoá mềm quá 30 ngày cho job purge.
UPDATE "Recipes" SET "IsDeleted" = TRUE, "DeletedAt" = now() - interval '40 days'
WHERE "Slug" LIKE 'bench-%' AND ("Slug" ~ '00$');

\echo
\echo '############################################################################'
\echo '#  TRƯỚC: bỏ các index mới của Chương 3 (chỉ trong transaction này)        #'
\echo '############################################################################'
DROP INDEX IF EXISTS "IX_Recipes_CreatedAt";
DROP INDEX IF EXISTS "IX_Recipes_CategoryId_Status_PublishedAt";
DROP INDEX IF EXISTS "IX_Recipes_DeletedAt_Purge";
ANALYZE "Recipes";

\echo
\echo '== [TRƯỚC] Q1. GET /recipes — mới nhất trước, trang 1 =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT r."Id", r."Slug", r."Title" FROM "Recipes" r
WHERE NOT r."IsDeleted" AND r."Status" = 1
ORDER BY r."CreatedAt" DESC, r."Id" LIMIT 12;

\echo
\echo '== [TRƯỚC] Q2. GET /categories/{slug} — công thức của 1 danh mục =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT r."Id", r."Slug", r."Title" FROM "Recipes" r
WHERE NOT r."IsDeleted" AND r."CategoryId" = (SELECT "Id" FROM "Categories" WHERE NOT "IsDeleted" ORDER BY "Id" LIMIT 1)
  AND r."Status" = 1
ORDER BY r."PublishedAt" DESC, r."Id" LIMIT 12;

\echo
\echo '== [TRƯỚC] Q3. Job purge — công thức xoá mềm quá 30 ngày =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT r."Id" FROM "Recipes" r WHERE r."IsDeleted" AND r."DeletedAt" < now() - interval '30 days';

\echo
\echo '############################################################################'
\echo '#  SAU: tạo lại index của Chương 3                                          #'
\echo '############################################################################'
CREATE INDEX IF NOT EXISTS "IX_Recipes_CreatedAt" ON "Recipes" ("CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS "IX_Recipes_CategoryId_Status_PublishedAt"
    ON "Recipes" ("CategoryId", "Status", "PublishedAt" DESC);
CREATE INDEX IF NOT EXISTS "IX_Recipes_DeletedAt_Purge" ON "Recipes" ("DeletedAt") WHERE "IsDeleted";
ANALYZE "Recipes";

\echo
\echo '== [SAU] Q1. GET /recipes — mới nhất trước, trang 1 =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT r."Id", r."Slug", r."Title" FROM "Recipes" r
WHERE NOT r."IsDeleted" AND r."Status" = 1
ORDER BY r."CreatedAt" DESC, r."Id" LIMIT 12;

\echo
\echo '== [SAU] Q2. GET /categories/{slug} — công thức của 1 danh mục =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT r."Id", r."Slug", r."Title" FROM "Recipes" r
WHERE NOT r."IsDeleted" AND r."CategoryId" = (SELECT "Id" FROM "Categories" WHERE NOT "IsDeleted" ORDER BY "Id" LIMIT 1)
  AND r."Status" = 1
ORDER BY r."PublishedAt" DESC, r."Id" LIMIT 12;

\echo
\echo '== [SAU] Q3. Job purge — công thức xoá mềm quá 30 ngày =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT r."Id" FROM "Recipes" r WHERE r."IsDeleted" AND r."DeletedAt" < now() - interval '30 days';

\echo
\echo '############################################################################'
\echo '#  Tìm kiếm (3.6.2): LIKE quét toàn bảng vs Full-Text Search dùng GIN       #'
\echo '############################################################################'
\echo
\echo '== Q4a. LIKE (KHÔNG dùng được index B-tree vì % ở đầu) =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT r."Id" FROM "Recipes" r
WHERE NOT r."IsDeleted" AND r."Status" = 1 AND unaccent(lower(r."Title")) LIKE '%pho bo%'
LIMIT 12;

\echo
\echo '== Q4b. Full-Text Search tsvector @@ websearch_to_tsquery + ts_rank (code thật của FR-SRCH-001) =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT r."Id" FROM "Recipes" r
WHERE NOT r."IsDeleted" AND r."Status" = 1
  AND r."SearchVector" @@ websearch_to_tsquery('vietnamese', unaccent('pho bo'))
ORDER BY ts_rank(r."SearchVector", websearch_to_tsquery('vietnamese', unaccent('pho bo'))) DESC, r."CreatedAt" DESC, r."Id"
LIMIT 12;

\echo
\echo '== Q5. GET /recipes/{slug} — chi tiết (unique index trên Slug) =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT r."Id" FROM "Recipes" r WHERE r."Slug" = 'bench-12345' AND NOT r."IsDeleted";

ROLLBACK;
\echo
\echo 'Đã ROLLBACK — dữ liệu giả và thay đổi index không còn trong database.'
