using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ECommerce.Core.Users;
using ECommerce.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ECommerce.IntegrationTests;

/// <summary>Boots the real API (all middleware, auth, validation) on an isolated in-memory SQLite database.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.dev";
    public const string AdminPassword = "Admin-Password-1";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "Data Source=:memory:");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:SeedDemoData", "true");
        builder.UseSetting("Database:AdminEmail", AdminEmail);
        builder.UseSetting("Database:AdminPassword", AdminPassword);
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("AI:Anthropic:ApiKey", "");
        builder.UseSetting("ANTHROPIC_API_KEY", "");
        builder.UseSetting("RateLimiting:AuthRequestsPerMinute", "1000");
        builder.UseSetting("RateLimiting:AiRequestsPerMinute", "5");

        builder.ConfigureServices(services =>
        {
            // Share one open connection so the in-memory database lives as long as the factory.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        });
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string? email = null, string password = "Password-123")
    {
        var client = CreateClient();
        AuthResponse auth;
        if (email == AdminEmail)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = AdminPassword });
            login.EnsureSuccessStatusCode();
            auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        }
        else
        {
            var register = await client.PostAsJsonAsync("/api/auth/register", new
            {
                email = email ?? $"user-{Guid.NewGuid():N}@test.dev",
                password,
                displayName = "Test User",
            });
            register.EnsureSuccessStatusCode();
            auth = (await register.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
