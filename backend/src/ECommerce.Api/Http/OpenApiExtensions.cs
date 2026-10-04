using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ECommerce.Api.Http;

internal static class OpenApiExtensions
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "ShopSense API",
                    Version = "v1",
                    Description = "E-commerce API with a Claude-powered shopping assistant.",
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Paste the accessToken from /api/auth/login.",
                };
                return Task.CompletedTask;
            });
        });
}
