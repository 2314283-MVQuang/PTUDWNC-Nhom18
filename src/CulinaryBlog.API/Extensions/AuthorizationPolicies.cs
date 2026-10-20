using Microsoft.AspNetCore.Authorization;

namespace CulinaryBlog.API.Extensions;

/// <summary>
/// Named policy — mục 5.2: "Named policy (\"AuthorPolicy\", \"AdminPolicy\" — không hardcode
/// string)". Endpoint gọi .RequireAuthorization(AuthorizationPolicies.Admin) thay vì
/// .RequireAuthorization("Admin") rải rác khắp nơi.
/// </summary>
public static class AuthorizationPolicies
{
    public const string Admin = "AdminPolicy";
    public const string Author = "AuthorPolicy";

    public static void AddCulinaryBlogPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Admin, policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
        options.AddPolicy(Author, policy => policy.RequireAuthenticatedUser().RequireRole("Author", "Admin"));
    }
}
