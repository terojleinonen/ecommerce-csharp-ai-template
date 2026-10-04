using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ECommerce.Api.Auth;
using ECommerce.Api.Endpoints;
using ECommerce.Api.Http;
using ECommerce.Core.Users;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
    builder.Logging.AddJsonConsole();

// ---------- Core services ----------
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddValidation();
builder.Services.AddProblemDetails(o =>
{
    o.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApiDocumentation();

// ---------- Authentication ----------
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = JwtTokenService.CreateKey(jwt.Value.SigningKey),
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = AuthConstants.RoleClaim,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthConstants.AdminPolicy, p => p.RequireRole(nameof(UserRole.Admin)));

// ---------- Rate limiting ----------
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string PartitionKey(HttpContext ctx) =>
        ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? ctx.Connection.RemoteIpAddress?.ToString()
        ?? "anonymous";

    var limits = builder.Configuration.GetSection("RateLimiting");
    o.AddPolicy(AssistantEndpoints.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        PartitionKey(ctx), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.GetValue("AiRequestsPerMinute", 10),
            Window = TimeSpan.FromMinutes(1),
        }));
    o.AddPolicy(AuthEndpoints.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.GetValue("AuthRequestsPerMinute", 10),
            Window = TimeSpan.FromMinutes(1),
        }));
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetTokenBucketLimiter(
        PartitionKey(ctx), _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = limits.GetValue("GlobalBurst", 200),
            TokensPerPeriod = limits.GetValue("GlobalPerSecond", 50),
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        }));
});

// ---------- Caching, CORS, health ----------
builder.Services.AddOutputCache(o =>
    o.AddPolicy(CatalogEndpoints.CachePolicy, p => p.Expire(TimeSpan.FromMinutes(5)).Tag(CatalogEndpoints.CacheTag)));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The API only runs behind our own reverse proxy (nginx / ingress) in container deployments.
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// ---------- Pipeline ----------
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

if (app.Configuration.GetValue("Api:EnableDocs", app.Environment.IsDevelopment()))
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("ShopSense API"));
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseOutputCache();

app.MapCatalogEndpoints();
app.MapAuthEndpoints();
app.MapOrderEndpoints();
app.MapAdminEndpoints();
app.MapAssistantEndpoints();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

await DatabaseInitializer.InitializeAsync(app.Services);
await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
