using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <summary>
    /// FR-FILE-001 / FR-JOB-002 (Tiến, Tuần 7): thay khối <c>CREATE TABLE IF NOT EXISTS "UploadedFiles"</c>
    /// từng chạy lúc API khởi động bằng migration. Viết bằng SQL idempotent (IF NOT EXISTS) vì database
    /// của nhóm có máy tạo bằng db/init/02-schema.sql, có máy đã có bảng do Program.cs cũ tạo — chạy
    /// "dotnet ef database update" ở trường hợp nào cũng ra đúng bảng, không lỗi "đã tồn tại".
    /// </summary>
    public partial class AddUploadedFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "UploadedFiles" (
                    "Id" uuid NOT NULL,
                    "FileName" character varying(255) NOT NULL,
                    "ContentType" character varying(100) NOT NULL,
                    "Size" bigint NOT NULL,
                    "StorageKey" character varying(500) NOT NULL,
                    "Url" character varying(1000) NOT NULL,
                    "BucketName" character varying(100) NOT NULL,
                    "UploadedBy" character varying(256) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
                    "DeletedAt" timestamp with time zone NULL,
                    "RowVersion" bytea NOT NULL DEFAULT '\x'::bytea,
                    CONSTRAINT "PK_UploadedFiles" PRIMARY KEY ("Id")
                );
                ALTER TABLE "UploadedFiles" ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;
                ALTER TABLE "UploadedFiles" ADD COLUMN IF NOT EXISTS "MediumUrl" character varying(1000) NULL;
                ALTER TABLE "UploadedFiles" ADD COLUMN IF NOT EXISTS "ThumbnailUrl" character varying(1000) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Chỉ gỡ 2 cột mới của FR-JOB-002; bảng gốc thuộc migration SyncModelChangesAfterMerge.
            migrationBuilder.Sql("""
                ALTER TABLE "UploadedFiles" DROP COLUMN IF EXISTS "MediumUrl";
                ALTER TABLE "UploadedFiles" DROP COLUMN IF EXISTS "ThumbnailUrl";
                """);
        }
    }
}
