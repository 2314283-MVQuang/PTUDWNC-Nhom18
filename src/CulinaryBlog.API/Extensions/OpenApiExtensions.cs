using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace CulinaryBlog.API.Extensions;

public static class OpenApiExtensions
{
    public static void MapCulinaryApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    public static IServiceCollection AddCulinaryOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddOperationTransformer(async (operation, context, ct) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var requiresAuth = metadata.OfType<IAuthorizeData>().Any()
                    && !metadata.OfType<IAllowAnonymous>().Any();
                operation.Summary ??= $"{context.Description.HttpMethod} /{context.Description.RelativePath}";
                operation.Responses ??= new OpenApiResponses();
                var schema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), cancellationToken: ct);
                void Error(string code, string description) => operation.Responses.TryAdd(code,
                    new OpenApiResponse
                    {
                        Description = description,
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/problem+json"] = new() { Schema = schema },
                        },
                    });
                Error("429", "RATE_LIMIT_EXCEEDED. Retry-After cho biết số giây cần đợi.");
                if (operation.Responses["429"] is OpenApiResponse rateLimitResponse)
                {
                    rateLimitResponse.Headers = new Dictionary<string, IOpenApiHeader>
                    {
                        ["Retry-After"] = new OpenApiHeader
                        {
                            Description = "Số giây trước khi thử lại.",
                            Schema = new OpenApiSchema { Type = JsonSchemaType.Integer },
                        },
                    };
                }
                Error("500", "Lỗi hệ thống; không trả stack trace.");
                if (context.Description.HttpMethod is "POST" or "PUT" or "PATCH")
                    Error("422", "VALIDATION_ERROR. Xem errors theo từng trường.");
                if (requiresAuth)
                {
                    operation.Security = [new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [],
                    }];
                    Error("401", "AUTH_TOKEN_INVALID: JWT thiếu, sai hoặc hết hạn.");
                    Error("403", "FORBIDDEN: không đủ role hoặc quyền sở hữu.");
                }
                if (context.Description.RelativePath?.EndsWith("/publish") == true)
                    Error("400", "RECIPE_PUBLISH_INCOMPLETE: cần ít nhất một bước và một nguyên liệu.");
                if (context.Description.HttpMethod == "PUT" && context.Description.RelativePath?.Contains("/recipes/") == true)
                    Error("409", "RECIPE_CONCURRENCY_CONFLICT: tải lại RowVersion trước khi sửa.");
                foreach (var parameter in operation.Parameters ?? [])
                    if (parameter is OpenApiParameter header && header.Name == "If-Match") header.Required = true;
            });
            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Culinary Blog API", Version = "1.0",
                    Description = "API tuần 1–5. Version nằm trong URL /api/v1. Endpoint ghi dùng Bearer JWT; PUT Recipe cần If-Match (RowVersion Base64).",
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                    },
                };
                // URL thực tế của tài liệu v1; bỏ tham số version khỏi phần nhập liệu của Scalar.
                foreach (var path in document.Paths.Keys.ToArray())
                {
                    if (!path.Contains("{version}")) continue;
                    var item = document.Paths[path];
                    document.Paths.Remove(path);
                    document.Paths[path.Replace("{version}", "1")] = item;
                    foreach (var operation in item.Operations?.Values.AsEnumerable() ?? [])
                    {
                        operation.Parameters = operation.Parameters?
                            .Where(parameter => parameter.Name != "version").ToList();
                    }
                }
                return Task.CompletedTask;
            });
        });
        return services;
    }
}
