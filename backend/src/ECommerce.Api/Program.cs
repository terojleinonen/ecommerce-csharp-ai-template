using ECommerce.Core.DTOs;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;
using ECommerce.Infrastructure.Repositories;
using ECommerce.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    // In-memory DB for template. Switch to SQL Server/PostgreSQL later.
    opt.UseInMemoryDatabase("ECommerceDb");
});

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IAIService, AIService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map endpoints
app.MapGet("/api/products", async (IProductRepository repo, CancellationToken ct) =>
{
    var products = await repo.GetAllAsync(ct);
    var dtos = products.Select(p => new ProductDto(p.Id, p.Sku, p.Name, p.Description, p.Price, p.ImageUrl, p.Category));
    return Results.Ok(dtos);
});

app.MapGet("/api/products/{id:int}", async (int id, IProductRepository repo, CancellationToken ct) =>
{
    var product = await repo.GetByIdAsync(id, ct);
    if (product is null) return Results.NotFound();
    var dto = new ProductDto(product.Id, product.Sku, product.Name, product.Description, product.Price, product.ImageUrl, product.Category);
    return Results.Ok(dto);
});

app.MapPost("/api/ai/describe-product", async (IAIService ai, ProductDto dto, CancellationToken ct) =>
{
    var desc = await ai.GenerateProductDescriptionAsync(dto.Name, dto.Description, ct);
    return Results.Ok(new { description = desc });
});

app.MapPost("/api/ai/chat", async (IAIService ai, ChatRequestDto request, CancellationToken ct) =>
{
    var reply = await ai.ChatAsync(request.Message, ct);
    return Results.Ok(new ChatResponseDto(reply));
});

app.Run();
