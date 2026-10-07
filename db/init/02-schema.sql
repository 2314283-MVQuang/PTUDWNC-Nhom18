-- Ép UTF-8 cho phiên làm việc: psql trên Windows mặc định dùng WIN1252 nên sẽ báo
-- "character with byte sequence 0x.. has no equivalent in encoding UTF8" khi gặp tiếng Việt.
SET client_encoding = 'UTF8';

-- =============================================================================
--  02 — SCHEMA: tạo toàn bộ bảng theo MỤC 7 của tài liệu đặc tả
--
--  Quy ước đặt tên: dùng PascalCase trong dấu nháy kép ("Recipes", "Title"...) cho
--  khớp với cách EF Core + Npgsql sinh SQL. Nếu bỏ nháy kép, PostgreSQL sẽ tự
--  chuyển hết thành chữ thường và backend sẽ không tìm thấy bảng.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- Hàm trigger dùng chung cho mọi bảng kế thừa BaseEntity (mục 7.1).
--
-- Mỗi lần UPDATE:
--   - UpdatedAt  = thời điểm hiện tại
--   - RowVersion = 8 byte ngẫu nhiên mới
--
-- RowVersion chính là "concurrency token" (mục 7.1): backend đọc recipe ra kèm
-- RowVersion, khi lưu sẽ gửi lại giá trị đó qua header If-Match. Nếu trong lúc đó
-- có người khác sửa thì RowVersion trong DB đã đổi → backend biết và trả lỗi
-- RECIPE_CONCURRENCY_CONFLICT thay vì ghi đè mất dữ liệu của người kia.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION touch_row()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW."UpdatedAt"  := now();
    NEW."RowVersion" := gen_random_bytes(8);
    RETURN NEW;
END;
$$;


-- =============================================================================
--  ASP.NET CORE IDENTITY (mục 7.7)
--  Backend dùng Identity nên phải đúng tên bảng/cột mà Identity mong đợi.
-- =============================================================================

CREATE TABLE IF NOT EXISTS "AspNetRoles" (
    "Id"               varchar(450) PRIMARY KEY,
    "Name"             varchar(256),
    "NormalizedName"   varchar(256),
    "ConcurrencyStamp" text
);
CREATE UNIQUE INDEX IF NOT EXISTS "RoleNameIndex"
    ON "AspNetRoles" ("NormalizedName");


CREATE TABLE IF NOT EXISTS "AspNetUsers" (
    "Id"                   varchar(450) PRIMARY KEY,
    "UserName"             varchar(256),
    "NormalizedUserName"   varchar(256),
    "Email"                varchar(256),
    "NormalizedEmail"      varchar(256),
    "EmailConfirmed"       boolean      NOT NULL DEFAULT false,
    "PasswordHash"         text,
    "SecurityStamp"        text,
    "ConcurrencyStamp"     text,
    "PhoneNumber"          text,
    "PhoneNumberConfirmed" boolean      NOT NULL DEFAULT false,
    "TwoFactorEnabled"     boolean      NOT NULL DEFAULT false,
    "LockoutEnd"           timestamptz,
    "LockoutEnabled"       boolean      NOT NULL DEFAULT true,
    "AccessFailedCount"    integer      NOT NULL DEFAULT 0,

    -- Cột tuỳ chỉnh thêm của ApplicationUser (mục 7.7)
    "DisplayName"          varchar(100) NOT NULL,
    "AvatarUrl"            varchar(500),
    "Bio"                  text,
    "IsActive"             boolean      NOT NULL DEFAULT true,
    "CreatedAt"            timestamptz  NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS "UserNameIndex"
    ON "AspNetUsers" ("NormalizedUserName");
CREATE INDEX IF NOT EXISTS "EmailIndex"
    ON "AspNetUsers" ("NormalizedEmail");


CREATE TABLE IF NOT EXISTS "AspNetRoleClaims" (
    "Id"         serial       PRIMARY KEY,
    "RoleId"     varchar(450) NOT NULL REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    "ClaimType"  text,
    "ClaimValue" text
);
CREATE INDEX IF NOT EXISTS "IX_AspNetRoleClaims_RoleId"
    ON "AspNetRoleClaims" ("RoleId");


CREATE TABLE IF NOT EXISTS "AspNetUserClaims" (
    "Id"         serial       PRIMARY KEY,
    "UserId"     varchar(450) NOT NULL REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    "ClaimType"  text,
    "ClaimValue" text
);
CREATE INDEX IF NOT EXISTS "IX_AspNetUserClaims_UserId"
    ON "AspNetUserClaims" ("UserId");


-- Bảng này lưu liên kết tài khoản Google OAuth (FR-AUTH-003).
CREATE TABLE IF NOT EXISTS "AspNetUserLogins" (
    "LoginProvider"       varchar(450) NOT NULL,
    "ProviderKey"         varchar(450) NOT NULL,
    "ProviderDisplayName" text,
    "UserId"              varchar(450) NOT NULL REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    PRIMARY KEY ("LoginProvider", "ProviderKey")
);
CREATE INDEX IF NOT EXISTS "IX_AspNetUserLogins_UserId"
    ON "AspNetUserLogins" ("UserId");


CREATE TABLE IF NOT EXISTS "AspNetUserRoles" (
    "UserId" varchar(450) NOT NULL REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    "RoleId" varchar(450) NOT NULL REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    PRIMARY KEY ("UserId", "RoleId")
);
CREATE INDEX IF NOT EXISTS "IX_AspNetUserRoles_RoleId"
    ON "AspNetUserRoles" ("RoleId");


CREATE TABLE IF NOT EXISTS "AspNetUserTokens" (
    "UserId"        varchar(450) NOT NULL REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    "LoginProvider" varchar(450) NOT NULL,
    "Name"          varchar(450) NOT NULL,
    "Value"         text,
    PRIMARY KEY ("UserId", "LoginProvider", "Name")
);


-- =============================================================================
--  CATEGORIES (mục 7.6)
-- =============================================================================

CREATE TABLE IF NOT EXISTS "Categories" (
    "Id"          uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name"        varchar(100) NOT NULL UNIQUE,
    "Slug"        varchar(120) NOT NULL UNIQUE,
    "Description" text,
    "ImageUrl"    varchar(500),
    "OrderIndex"  integer      NOT NULL DEFAULT 0,

    -- BaseEntity (mục 7.1)
    "CreatedAt"   timestamptz  NOT NULL DEFAULT now(),
    "UpdatedAt"   timestamptz,
    "IsDeleted"   boolean      NOT NULL DEFAULT false,
    "DeletedAt"   timestamptz,
    "RowVersion"  bytea        NOT NULL DEFAULT gen_random_bytes(8)
);

DROP TRIGGER IF EXISTS "trg_Categories_touch" ON "Categories";
CREATE TRIGGER "trg_Categories_touch"
    BEFORE UPDATE ON "Categories"
    FOR EACH ROW EXECUTE FUNCTION touch_row();


-- =============================================================================
--  RECIPES (mục 7.2) — bảng lõi của hệ thống
--
--  Các cột Nutrition_* là "Owned Entity" RecipeNutrition: EF Core nhúng thẳng vào
--  bảng Recipes với tiền tố Nutrition_ chứ không tạo bảng riêng.
-- =============================================================================

CREATE TABLE IF NOT EXISTS "Recipes" (
    "Id"                     uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    "Title"                  varchar(200) NOT NULL,
    "Slug"                   varchar(220) NOT NULL UNIQUE,
    "Description"            text         NOT NULL,
    "Instructions"           text         NOT NULL,
    "PrepTime"               integer      NOT NULL CHECK ("PrepTime" > 0),
    "CookTime"               integer      NOT NULL CHECK ("CookTime" >= 0),
    "Servings"               integer      NOT NULL CHECK ("Servings" > 0),
    -- 1=Easy 2=Medium 3=Hard 4=Expert
    "Difficulty"             smallint     NOT NULL DEFAULT 1 CHECK ("Difficulty" BETWEEN 1 AND 4),
    -- 0=Draft 1=Published 2=Archived
    "Status"                 smallint     NOT NULL DEFAULT 0 CHECK ("Status" BETWEEN 0 AND 2),

    -- ON DELETE RESTRICT: không cho xoá danh mục đang có công thức (CATEGORY_DELETE_HAS_RECIPES)
    "CategoryId"             uuid         NOT NULL REFERENCES "Categories" ("Id") ON DELETE RESTRICT,
    "AuthorId"               varchar(450) NOT NULL REFERENCES "AspNetUsers" ("Id"),

    "SearchVector"           tsvector,
    "PublishedAt"            timestamptz,

    -- RecipeNutrition (Owned Entity) — tất cả nullable, tính trên 1 khẩu phần
    "Nutrition_Calories"      decimal(8,2),
    "Nutrition_Protein"       decimal(8,2),
    "Nutrition_Carbohydrates" decimal(8,2),
    "Nutrition_Fat"           decimal(8,2),
    "Nutrition_Fiber"         decimal(8,2),
    "Nutrition_Sodium"        decimal(8,2),

    -- BaseEntity
    "CreatedAt"              timestamptz  NOT NULL DEFAULT now(),
    "UpdatedAt"              timestamptz,
    "IsDeleted"              boolean      NOT NULL DEFAULT false,
    "DeletedAt"              timestamptz,
    "RowVersion"             bytea        NOT NULL DEFAULT gen_random_bytes(8)
);

CREATE INDEX IF NOT EXISTS "IX_Recipes_CategoryId"  ON "Recipes" ("CategoryId");
CREATE INDEX IF NOT EXISTS "IX_Recipes_AuthorId"    ON "Recipes" ("AuthorId");
CREATE INDEX IF NOT EXISTS "IX_Recipes_Status"      ON "Recipes" ("Status");
CREATE INDEX IF NOT EXISTS "IX_Recipes_Difficulty"  ON "Recipes" ("Difficulty");
CREATE INDEX IF NOT EXISTS "IX_Recipes_PublishedAt" ON "Recipes" ("PublishedAt" DESC);

-- GIN index cho full-text search — thứ làm cho câu tìm kiếm nhanh dù có hàng triệu dòng.
CREATE INDEX IF NOT EXISTS "IX_Recipes_SearchVector"
    ON "Recipes" USING GIN ("SearchVector");

-- Trigram index: hỗ trợ tìm gần đúng theo tiêu đề (gõ thiếu/sai vài ký tự vẫn ra).
CREATE INDEX IF NOT EXISTS "IX_Recipes_Title_trgm"
    ON "Recipes" USING GIN ("Title" gin_trgm_ops);


-- -----------------------------------------------------------------------------
-- Trigger cập nhật SearchVector mỗi khi Title/Description thay đổi (mục 7.2).
-- setweight 'A' cho tiêu đề, 'B' cho mô tả → khi xếp hạng kết quả, trùng ở tiêu đề
-- được tính điểm cao hơn trùng ở mô tả.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION recipes_search_vector_update()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW."SearchVector" :=
        setweight(to_tsvector('vietnamese', coalesce(NEW."Title", '')),       'A') ||
        setweight(to_tsvector('vietnamese', coalesce(NEW."Description", '')), 'B');
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS "trg_Recipes_search_vector" ON "Recipes";
CREATE TRIGGER "trg_Recipes_search_vector"
    BEFORE INSERT OR UPDATE OF "Title", "Description" ON "Recipes"
    FOR EACH ROW EXECUTE FUNCTION recipes_search_vector_update();

DROP TRIGGER IF EXISTS "trg_Recipes_touch" ON "Recipes";
CREATE TRIGGER "trg_Recipes_touch"
    BEFORE UPDATE ON "Recipes"
    FOR EACH ROW EXECUTE FUNCTION touch_row();


-- =============================================================================
--  RECIPE STEPS (mục 7.3)
-- =============================================================================

CREATE TABLE IF NOT EXISTS "RecipeSteps" (
    "Id"           uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    "RecipeId"     uuid         NOT NULL REFERENCES "Recipes" ("Id") ON DELETE CASCADE,
    "StepNumber"   integer      NOT NULL CHECK ("StepNumber" > 0),
    "Title"        varchar(200) NOT NULL,
    "Description"  text         NOT NULL,
    "TimerMinutes" integer      CHECK ("TimerMinutes" IS NULL OR "TimerMinutes" >= 0),
    "ImageUrl"     varchar(500),

    "CreatedAt"    timestamptz  NOT NULL DEFAULT now(),
    "UpdatedAt"    timestamptz,
    "IsDeleted"    boolean      NOT NULL DEFAULT false,
    "DeletedAt"    timestamptz,
    "RowVersion"   bytea        NOT NULL DEFAULT gen_random_bytes(8),

    -- Trong 1 công thức không được có 2 bước cùng số thứ tự
    CONSTRAINT "UQ_RecipeSteps_RecipeId_StepNumber" UNIQUE ("RecipeId", "StepNumber")
);
CREATE INDEX IF NOT EXISTS "IX_RecipeSteps_RecipeId" ON "RecipeSteps" ("RecipeId");

DROP TRIGGER IF EXISTS "trg_RecipeSteps_touch" ON "RecipeSteps";
CREATE TRIGGER "trg_RecipeSteps_touch"
    BEFORE UPDATE ON "RecipeSteps"
    FOR EACH ROW EXECUTE FUNCTION touch_row();


-- =============================================================================
--  RECIPE INGREDIENTS (mục 7.4)
-- =============================================================================

CREATE TABLE IF NOT EXISTS "RecipeIngredients" (
    "Id"         uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
    "RecipeId"   uuid          NOT NULL REFERENCES "Recipes" ("Id") ON DELETE CASCADE,
    "Name"       varchar(200)  NOT NULL,
    "Quantity"   decimal(10,3),
    "Unit"       varchar(50),
    "Notes"      varchar(500),
    "OrderIndex" integer       NOT NULL DEFAULT 0,

    "CreatedAt"  timestamptz   NOT NULL DEFAULT now(),
    "UpdatedAt"  timestamptz,
    "IsDeleted"  boolean       NOT NULL DEFAULT false,
    "DeletedAt"  timestamptz,
    "RowVersion" bytea         NOT NULL DEFAULT gen_random_bytes(8)
);
CREATE INDEX IF NOT EXISTS "IX_RecipeIngredients_RecipeId"
    ON "RecipeIngredients" ("RecipeId");

DROP TRIGGER IF EXISTS "trg_RecipeIngredients_touch" ON "RecipeIngredients";
CREATE TRIGGER "trg_RecipeIngredients_touch"
    BEFORE UPDATE ON "RecipeIngredients"
    FOR EACH ROW EXECUTE FUNCTION touch_row();


-- =============================================================================
--  RECIPE IMAGES (mục 7.5)
-- =============================================================================

CREATE TABLE IF NOT EXISTS "RecipeImages" (
    "Id"           uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    "RecipeId"     uuid         NOT NULL REFERENCES "Recipes" ("Id") ON DELETE CASCADE,
    "OriginalUrl"  varchar(500) NOT NULL,
    "MediumUrl"    varchar(500),
    "ThumbnailUrl" varchar(500),
    "AltText"      varchar(200),
    "IsPrimary"    boolean      NOT NULL DEFAULT false,
    "OrderIndex"   integer      NOT NULL DEFAULT 0,

    "CreatedAt"    timestamptz  NOT NULL DEFAULT now(),
    "UpdatedAt"    timestamptz,
    "IsDeleted"    boolean      NOT NULL DEFAULT false,
    "DeletedAt"    timestamptz,
    "RowVersion"   bytea        NOT NULL DEFAULT gen_random_bytes(8)
);
CREATE INDEX IF NOT EXISTS "IX_RecipeImages_RecipeId" ON "RecipeImages" ("RecipeId");

-- Mỗi công thức chỉ được có ĐÚNG 1 ảnh đại diện. Đây là "partial unique index":
-- ràng buộc chỉ áp dụng cho các dòng có IsPrimary = true.
CREATE UNIQUE INDEX IF NOT EXISTS "UQ_RecipeImages_OnePrimaryPerRecipe"
    ON "RecipeImages" ("RecipeId") WHERE "IsPrimary";

DROP TRIGGER IF EXISTS "trg_RecipeImages_touch" ON "RecipeImages";
CREATE TRIGGER "trg_RecipeImages_touch"
    BEFORE UPDATE ON "RecipeImages"
    FOR EACH ROW EXECUTE FUNCTION touch_row();


-- =============================================================================
--  REFRESH TOKENS (mục 7.8)
--  Chỉ lưu SHA-256 hash của token, không bao giờ lưu token gốc.
-- =============================================================================

CREATE TABLE IF NOT EXISTS "RefreshTokens" (
    "Id"                   uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId"               varchar(450) NOT NULL REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    "TokenHash"            varchar(64)  NOT NULL UNIQUE,
    "ExpiresAt"            timestamptz  NOT NULL,
    "RevokedAt"            timestamptz,
    "ReplacedByTokenHash"  varchar(64),
    "CreatedAt"            timestamptz  NOT NULL DEFAULT now(),
    "CreatedByIp"          varchar(45)
);
CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_ExpiresAt" ON "RefreshTokens" ("ExpiresAt");
