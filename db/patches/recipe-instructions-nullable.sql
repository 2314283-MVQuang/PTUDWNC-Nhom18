-- Chung Thien Y, tuan 3: dong bo quyet dinh SRS muc 10.
-- Ap dung mot lan cho database da duoc tao bang db/init/*.sql.
-- Co the chay lai; chi bo rang buoc NOT NULL, giu nguyen gia tri hien co.
BEGIN;
ALTER TABLE "Recipes" ALTER COLUMN "Instructions" DROP NOT NULL;
COMMIT;
