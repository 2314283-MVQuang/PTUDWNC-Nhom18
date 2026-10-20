using System.Text.Json.Serialization;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.API.Services;
using CulinaryBlog.Application;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Hangfire;
using Hangfire.PostgreSql;
using CulinaryBlog.Infrastructure.Jobs;

// Bootstrap logger: bắt lỗi xảy ra TRƯỚC khi builder.Host.UseSerilog() đọc được cấu hình từ
// appsettings (vd sai connection string lúc AddDbContext) — không có bootstrap logger thì lỗi
// giai đoạn này chỉ hiện ra console mặc định, không ghi log có cấu trúc được.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// FR-OBS-002 (Tuần 3): Serilog thay Microsoft.Extensions.Logging mặc định — đọc cấu hình từ
// section "Serilog" trong appsettings.json (sink Console + Seq, xem docker-compose.yml service
// "seq"). ReadFrom.Services(services) cho phép Serilog dùng Enricher đăng ký qua DI nếu cần sau.
// ---------------------------------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ---------------------------------------------------------------------------
// Đăng ký service — mỗi tầng có 1 extension method DI riêng (mục 6.2: Program.cs +
// extension methods AddApplication/AddInfrastructure/AddPresentation).
// ---------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------
// Tuần 4: Output Cache backed bởi Redis (mâu thuẫn #3 — chỉ 1 tầng cache duy nhất, bỏ hẳn
// IMemoryCache/CachingBehavior/CacheInvalidationBehavior khỏi mọi thiết kế về sau). Đăng ký ở đây
// (API layer, Sdk.Web) chứ không phải Infrastructure — Infrastructure là class library thường,
// không chắc có sẵn shared framework ASP.NET Core mà AddOutputCache()/OutputCachePolicyBuilder
// cần. Policy TTL lấy đúng NFR-PERF-003 làm chuẩn duy nhất; người làm Category/Recipe/Search chỉ
// cần gắn [OutputCache(PolicyName = "categories"|"RecipeDetail"|"Search")] (hoặc .CacheOutput(...)
// như CategoryEndpoints.cs) lên endpoint GET của mình — KHÔNG tự cấu hình cache riêng. Invalidate
// khi Create/Update/Delete: inject IOutputCacheStore rồi gọi
// EvictByTagAsync("categories"|"recipes"|"search", ct) trong Command Handler tương ứng (tag đặt
// sẵn trong policy dưới đây).
// ---------------------------------------------------------------------------
var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
    ?? throw new InvalidOperationException(
        "Thiếu cấu hình Redis:ConnectionString (xem appsettings.json / docker-compose.yml service \"redis\").");

builder.Services.AddStackExchangeRedisOutputCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "culinaryblog:";
});

// FR-CAT-003/004 (Tiến): Handler ở Application layer xoá cache theo tag qua interface này
// (cài đặt bằng IOutputCacheStore.EvictByTagAsync — mâu thuẫn #3/#4: evict tag khi Create/Update).
builder.Services.AddScoped<ICacheInvalidator, OutputCacheInvalidator>();

builder.Services.AddOutputCache(options =>
{
    // Category list: TTL 30 phút (NFR-PERF-003, "ít thay đổi"). Tên policy "categories" (chữ
    // thường) khớp với .CacheOutput("categories") ở CategoryEndpoints.cs (FR-CAT-001) — ĐỪNG đổi
    // tên nếu không sửa luôn bên đó.
    options.AddPolicy("categories", policy => policy
        .Expire(TimeSpan.FromMinutes(30))
        .Tag("categories"));

    // Category detail kèm danh sách công thức (FR-CAT-002): TTL 5 phút vì có chứa công thức (thay đổi
    // thường xuyên hơn danh mục), vary theo page/pageSize; gắn cả 2 tag để thao tác trên danh mục
    // (tạo/sửa/xoá) lẫn công thức (lưu trữ, thêm ảnh...) đều làm mới được trang này.
    options.AddPolicy("CategoryDetail", policy => policy
        .Expire(TimeSpan.FromMinutes(5))
        .SetVaryByQuery("page", "pageSize")
        .Tag("categories", "recipes"));

    // Recipe detail: TTL 5 phút (NFR-PERF-003, cache-aside pattern).
    options.AddPolicy("RecipeDetail", policy => policy
        .Expire(TimeSpan.FromMinutes(5))
        .Tag("recipes"));

    // Search: TTL 1 phút, vary theo TOÀN BỘ query string (q, category, sort, page...) — 2 query
    // khác nhau không được dùng chung 1 bản cache (NFR-PERF-003 + mâu thuẫn #3).
    options.AddPolicy("Search", policy => policy
        .Expire(TimeSpan.FromMinutes(1))
        .SetVaryByQuery("*")
        .Tag("search"));
});

// ---------------------------------------------------------------------------
// Tuần 5 (FR-JOB-003): Hangfire Background Jobs
// ---------------------------------------------------------------------------
var hangfireConnStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings();

    var useInMemory = true;
    if (!string.IsNullOrEmpty(hangfireConnStr))
    {
        try
        {
            using var tcpClient = new System.Net.Sockets.TcpClient();
            var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder(hangfireConnStr);
            var host = npgsqlBuilder.Host ?? "localhost";
            var port = npgsqlBuilder.Port > 0 ? npgsqlBuilder.Port : 5432;

            var connectTask = tcpClient.ConnectAsync(host, port);
            if (connectTask.Wait(TimeSpan.FromMilliseconds(400)) && tcpClient.Connected)
            {
                useInMemory = false;
            }
        }
        catch
        {
            useInMemory = true;
        }
    }

    if (!useInMemory)
    {
        config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(hangfireConnStr));
    }
    else
    {
        config.UseInMemoryStorage();
    }
});
builder.Services.AddHangfireServer();

builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddCulinaryApiVersioning();
builder.Services.AddCulinaryOpenApi();

// Cho phép body JSON gửi/nhận enum dạng chuỗi (vd "Easy" thay vì số 1) — dễ đọc hơn khi test API.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ---------------------------------------------------------------------------
// FR-OBS-003 (Tuần 3): OpenTelemetry tracing cơ bản — instrument request HTTP đến (ASP.NET Core)
// và HttpClient đi ra, cộng thêm trace các câu lệnh SQL qua Npgsql (cách thực tế để "thấy" EF
// Core, vì EF Core không tự phát Activity riêng — mọi lệnh SQL cuối cùng đều qua Npgsql). Npgsql
// tự phát Activity dưới ActivitySource tên "Npgsql" SẴN (từ Npgsql 6+, không cần gói riêng) —
// AddSource("Npgsql") chỉ là "đăng ký nghe" nguồn đó, KHÔNG dùng AddNpgsql() (tên đó là của EF Core
// Npgsql.EntityFrameworkCore.PostgreSQL, dùng để đăng ký DbContext, khác hoàn toàn mục đích).
// Có cấu hình "OpenTelemetry:OtlpEndpoint" (vd collector/Tempo) → xuất qua OTLP; KHÔNG có (mặc
// định máy dev chưa dựng collector) → in trace ra console để vẫn xem được, không mất tác dụng.
// ---------------------------------------------------------------------------
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "CulinaryBlog.API"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Npgsql");

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
        else
        {
            tracing.AddConsoleExporter();
        }
    });

var app = builder.Build();

// ---------------------------------------------------------------------------
// RoleSeeder (Tuần 3 — "phân quyền Role"): tạo sẵn role "Admin"/"Author" trong AspNetRoles nếu
// CHƯA có. PHẢI chạy ở MỌI environment (khác DbSeeder ở dưới, chỉ chạy Development) — thiếu
// bước này thì RegisterCommandHandler.AddToRoleAsync(user, "Author") sẽ lỗi ngay cả ở production.
// ---------------------------------------------------------------------------
try
{
    using var roleSeedScope = app.Services.CreateScope();
    // FR-FILE-001 (Tiến): bảng metadata file upload. Database của nhóm có máy tạo bằng script SQL,
    // có máy tạo bằng migration, nên tạo bảng bằng lệnh idempotent (IF NOT EXISTS) lúc khởi động để
    // máy nào cũng chạy được module Files. Khi nhóm chốt dùng EF Migrations thì thay bằng
    // "dotnet ef migrations add AddUploadedFiles" rồi xoá khối này.
    var filesDb = roleSeedScope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
    await filesDb.Database.ExecuteSqlRawAsync("""
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
        """);

    // FR-RCP-007 (Thịnh): BaseEntity có thêm cột "DeletedAt" (thời điểm xoá mềm, dùng cho job purge
    // 30 ngày). db/init/02-schema.sql chỉ chạy khi tạo database MỚI, nên database đã có sẵn trên máy
    // các thành viên phải được thêm cột ở đây — thiếu cột này thì mọi truy vấn vào 6 bảng dưới lỗi.
    await filesDb.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Categories"        ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;
        ALTER TABLE "Recipes"           ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;
        ALTER TABLE "RecipeSteps"       ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;
        ALTER TABLE "RecipeIngredients" ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;
        ALTER TABLE "RecipeImages"      ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;
        ALTER TABLE "UploadedFiles"     ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;
        """);

    var roleManager = roleSeedScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await RoleSeeder.SeedRolesAsync(roleManager, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Chưa thể kết nối tới cơ sở dữ liệu PostgreSQL khi khởi động (bỏ qua trong chế độ offline/dev test).");
}

// ---------------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------------
app.UseMiddleware<CorrelationIdMiddleware>(); // Đứng TRƯỚC GlobalException — request lỗi 500 vẫn cần có CorrelationId trong log.
app.UseMiddleware<GlobalExceptionMiddleware>(); // Phải đứng ĐẦU pipeline (sau CorrelationId) để bắt mọi exception phía sau.

app.MapCulinaryApiDocumentation();
if (app.Environment.IsDevelopment())
{
    // Sinh dữ liệu mẫu ngẫu nhiên bằng Bogus (chỉ ở Development, KHÔNG bao giờ chạy ở production).
    // DbSeeder tự kiểm tra số lượng hiện có và chỉ chèn thêm cho tới khi đạt tối thiểu 20
    // categories / 100 recipes (mỗi recipe >= 10 nguyên liệu, >= 5 bước) — xem
    // Infrastructure/Persistence/Seed/DbSeeder.cs. An toàn khi chạy lại nhiều lần.
    // GIỮ LẠI theo yêu cầu Lab: đảm bảo database luôn có đủ dữ liệu mẫu, dù các module CRUD
    // Category/Recipe (FR-CAT, FR-RCP...) đã tạm gỡ khỏi phạm vi triển khai để phân công lại.
    try
    {
        using var seedScope = app.Services.CreateScope();
        var seedContext = seedScope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        await DbSeeder.SeedRandomDataAsync(seedContext, app.Logger);

        // Tài khoản Admin THẬT trong DB (chỉ Development) để test API cần quyền Admin.
        var userManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await RoleSeeder.SeedDevAdminAsync(userManager, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Chưa thể kết nối tới cơ sở dữ liệu PostgreSQL để seed dữ liệu dev.");
    }
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Tuần 4: Output Cache middleware (backend Redis, đăng ký ở khối AddStackExchangeRedisOutputCache/
// AddOutputCache phía trên). Đứng SAU UseAuthorization để endpoint cache vẫn tôn trọng phân quyền
// trước khi phục vụ từ cache — chỉ endpoint nào tự gắn [OutputCache(PolicyName = "...")] mới bị
// cache, các endpoint Auth/health ở dưới KHÔNG bị ảnh hưởng vì không gắn policy nào.
app.UseOutputCache();

// Tuần 5 (FR-JOB-003): Hangfire Dashboard & Recurring Job sinh sitemap lúc 02:00 AM hàng ngày
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // Cho phép truy cập dashboard trong môi trường Development/Testing
    Authorization = []
});

RecurringJob.AddOrUpdate<GenerateSitemapJob>(
    "generate-sitemap",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 2 * * *"); // 02:00 AM hàng ngày (NFR-SEO, FR-JOB-003)

// FR-JOB-004 (Thịnh, mâu thuẫn #1): xoá hẳn công thức đã xoá mềm quá 30 ngày, chạy 03:00 AM hàng
// ngày (sau job sitemap để sitemap của ngày đó không còn chứa công thức sắp bị xoá).
RecurringJob.AddOrUpdate<PurgeDeletedRecipesJob>(
    "purge-deleted-recipes",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 3 * * *");

app.MapAuthEndpoints();
app.MapRecipeEndpoints();
app.MapRecipeItemEndpoints(); // FR-RCP-008/009/010 (Tiến): nguyên liệu, bước, ảnh
app.MapCategoryEndpoints();
app.MapFileEndpoints();       // FR-FILE-001 (Tiến): presigned URL MinIO
app.MapHealthEndpoints();
app.MapSitemapEndpoints();    // FR-JOB-003 (Tiến): GET /sitemap.xml

app.Run();
