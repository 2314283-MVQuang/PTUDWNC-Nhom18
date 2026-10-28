using System.Diagnostics.Metrics;

namespace CulinaryBlog.Application.Common.Diagnostics;

/// <summary>
/// Tuần 8 (Tiến) — FR-OBS-003 "custom business metrics (recipe created/published count)".
/// Dùng System.Diagnostics.Metrics có sẵn trong .NET (Application layer không phụ thuộc gói
/// OpenTelemetry nào); Program.cs đăng ký <c>AddMeter(RecipeMetrics.MeterName)</c> để xuất qua OTLP.
/// </summary>
public static class RecipeMetrics
{
    public const string MeterName = "CulinaryBlog";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> Created = Meter.CreateCounter<long>(
        "culinaryblog.recipes.created", unit: "{recipe}", description: "Số công thức được tạo mới.");

    private static readonly Counter<long> Published = Meter.CreateCounter<long>(
        "culinaryblog.recipes.published", unit: "{recipe}", description: "Số lần công thức chuyển sang Published.");

    public static void RecipeCreated() => Created.Add(1);

    public static void RecipePublished() => Published.Add(1);
}
