using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Core.AI;
using ECommerce.Core.Catalog;
using ECommerce.Core.Common;
using ECommerce.Core.Orders;
using ECommerce.Core.Users;

namespace ECommerce.IntegrationTests;

public sealed class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = ApiFactory.Json;

    private static object Address => new
    {
        fullName = "Ada Lovelace", line1 = "Mannerheimintie 1", postalCode = "00100", city = "Helsinki", country = "FI",
    };

    [Fact]
    public async Task Health_endpoints_report_healthy()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal("Healthy", await client.GetStringAsync("/health/ready"));
    }

    [Fact]
    public async Task Catalog_is_seeded_searchable_and_sortable()
    {
        var client = factory.CreateClient();

        var categories = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories", Json);
        Assert.Equal(5, categories!.Count);

        var search = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?search=coffee", Json);
        Assert.Equal("Pour-Over Coffee Set", search!.Items[0].Name);

        var sorted = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?sort=priceDesc&pageSize=3", Json);
        Assert.Equal(sorted!.Items.Select(p => p.Price).OrderByDescending(p => p), sorted.Items.Select(p => p.Price));
        Assert.Equal(20, sorted.TotalCount);
    }

    [Fact]
    public async Task Responses_include_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("/api/categories");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task Unknown_product_returns_problem_details()
    {
        var response = await factory.CreateClient().GetAsync("/api/products/424242");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_found", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Register_validates_input()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = "not-an-email", password = "short", displayName = "A" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task Duplicate_registration_and_bad_login_are_rejected()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.dev";
        await factory.CreateAuthenticatedClientAsync(email);
        var client = factory.CreateClient();

        var dup = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "Password-123", displayName = "Dup" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        var badLogin = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
    }

    [Fact]
    public async Task Me_returns_current_user()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me", Json);
        Assert.Equal(UserRole.Customer, me!.Role);
    }

    [Fact]
    public async Task Checkout_requires_authentication()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/orders",
            new { items = new[] { new { productId = 1, quantity = 1 } }, shippingAddress = Address });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_can_place_and_view_orders_with_server_side_prices()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var product = await client.GetFromJsonAsync<ProductDto>("/api/products/13", Json); // coffee set

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            // A malicious client can't influence the price: the API ignores anything but id + quantity.
            items = new[] { new { productId = 13, quantity = 2, unitPrice = 0.01 } },
            shippingAddress = Address,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(Json);
        Assert.Equal(product!.Price * 2, order!.Subtotal);
        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal($"/api/orders/{order.Id}", response.Headers.Location!.OriginalString);

        var mine = await client.GetFromJsonAsync<List<OrderDto>>("/api/orders", Json);
        Assert.Contains(mine!, o => o.Id == order.Id);

        // Another customer can't see it.
        var other = await factory.CreateAuthenticatedClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/orders/{order.Id}")).StatusCode);
    }

    [Fact]
    public async Task Checkout_rejects_out_of_stock_items_and_invalid_addresses()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var outOfStock = await client.PostAsJsonAsync("/api/orders",
            new { items = new[] { new { productId = 20, quantity = 1 } }, shippingAddress = Address });
        Assert.Equal(HttpStatusCode.BadRequest, outOfStock.StatusCode);
        Assert.Equal("insufficient_stock", (await outOfStock.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var badAddress = await client.PostAsJsonAsync("/api/orders", new
        {
            items = new[] { new { productId = 1, quantity = 1 } },
            shippingAddress = new { fullName = "A", line1 = "x", postalCode = "1", city = "y", country = "Finland" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, badAddress.StatusCode);
    }

    [Fact]
    public async Task Admin_endpoints_are_forbidden_for_customers()
    {
        var customer = await factory.CreateAuthenticatedClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/admin/products")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_manage_products_and_cache_is_invalidated()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(ApiFactory.AdminEmail);
        var anonymous = factory.CreateClient();

        // Prime the output cache.
        var before = await anonymous.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?search=kayak", Json);
        Assert.Equal(0, before!.TotalCount);

        var create = await admin.PostAsJsonAsync("/api/admin/products", new
        {
            sku = "OUT-KAYAK-9", name = "Inflatable Kayak", description = "Two-person kayak.",
            price = 399.0, categoryId = 5, stockQuantity = 4, isActive = true,
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ProductDto>(Json);

        var after = await anonymous.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?search=kayak", Json);
        Assert.Equal(created!.Id, Assert.Single(after!.Items).Id);

        var invalid = await admin.PostAsJsonAsync("/api/admin/products", new { sku = "bad sku", name = "X", price = 0, categoryId = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/products/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/products/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_generate_copy_and_update_order_status()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(ApiFactory.AdminEmail);

        var copy = await admin.PostAsJsonAsync("/api/admin/ai/product-description",
            new { name = "Trail Socks", categoryId = 1, price = 15.0 });
        copy.EnsureSuccessStatusCode();
        var generated = await copy.Content.ReadFromJsonAsync<GeneratedDescriptionDto>(Json);
        Assert.Equal("offline", generated!.Provider);
        Assert.Contains("Trail Socks", generated.Description, StringComparison.Ordinal);

        var customer = await factory.CreateAuthenticatedClientAsync();
        var order = await (await customer.PostAsJsonAsync("/api/orders",
                new { items = new[] { new { productId = 2, quantity = 1 } }, shippingAddress = Address }))
            .Content.ReadFromJsonAsync<OrderDto>(Json);

        var shipped = await admin.PatchAsJsonAsync($"/api/admin/orders/{order!.Id}/status", new { status = "shipped" });
        Assert.Equal(OrderStatus.Shipped, (await shipped.Content.ReadFromJsonAsync<OrderDto>(Json))!.Status);

        var invalid = await admin.PatchAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = "placed" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Assistant_answers_from_the_catalog_and_reports_status()
    {
        var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<AssistantStatusDto>("/api/assistant/status", Json);
        Assert.False(status!.LlmEnabled);

        var response = await client.PostAsJsonAsync("/api/assistant/chat", new
        {
            messages = new[] { new { role = "user", content = "Do you have noise-cancelling headphones?" } },
        });
        response.EnsureSuccessStatusCode();
        var reply = await response.Content.ReadFromJsonAsync<AssistantChatResponse>(Json);
        Assert.Equal("Noise-Cancelling Headphones", reply!.Products[0].Name);
    }

    [Fact]
    public async Task Assistant_rejects_conversations_not_ending_with_user()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/assistant/chat", new
        {
            messages = new[] { new { role = "assistant", content = "Hello!" } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class RateLimitTests
{
    [Fact]
    public async Task Assistant_is_rate_limited()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var body = new { messages = new[] { new { role = "user", content = "hi" } } };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 7; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/assistant/chat", body)).StatusCode);

        Assert.Equal(5, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
