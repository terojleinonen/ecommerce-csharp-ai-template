using Anthropic;
using ECommerce.Core.AI;
using ECommerce.Core.Catalog;
using ECommerce.Core.Orders;
using ECommerce.Core.Users;
using ECommerce.Infrastructure.AI;
using ECommerce.Infrastructure.Data;
using ECommerce.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddDbContext<AppDbContext>(opt =>
        {
            if (dbOptions.Provider == DatabaseProvider.Sqlite)
                opt.UseSqlite(connectionString);
            else
                opt.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3));
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddAiServices(configuration);
        return services;
    }

    private static void AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AnthropicOptions>()
            .Bind(configuration.GetSection(AnthropicOptions.SectionName))
            .PostConfigure(o =>
            {
                if (string.IsNullOrWhiteSpace(o.ApiKey))
                    o.ApiKey = configuration["ANTHROPIC_API_KEY"];
            });

        services.AddScoped<OfflineShoppingAssistant>();

        services.AddSingleton(sp => new AnthropicClient
        {
            ApiKey = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value.ApiKey,
            // A shopper is waiting on the other end: fail fast and let the offline assistant answer.
            Timeout = TimeSpan.FromSeconds(60),
            MaxRetries = 2,
        });

        // Resolve the provider per request so configuration is read once at first use.
        services.AddScoped<IShoppingAssistant>(sp =>
            sp.GetRequiredService<IOptions<AnthropicOptions>>().Value.IsConfigured
                ? ActivatorUtilities.CreateInstance<ClaudeShoppingAssistant>(sp)
                : sp.GetRequiredService<OfflineShoppingAssistant>());

        services.AddScoped<IProductCopywriter>(sp =>
            sp.GetRequiredService<IOptions<AnthropicOptions>>().Value.IsConfigured
                ? ActivatorUtilities.CreateInstance<ClaudeProductCopywriter>(sp)
                : new OfflineProductCopywriter());
    }
}
