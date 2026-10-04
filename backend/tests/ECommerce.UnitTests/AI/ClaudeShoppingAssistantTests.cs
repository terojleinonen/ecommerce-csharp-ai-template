using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using ECommerce.Core.AI;
using ECommerce.Infrastructure.AI;
using ECommerce.Infrastructure.Services;
using ECommerce.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.UnitTests.AI;

/// <summary>
/// Drives the real Anthropic SDK against a scripted HTTP handler, verifying the tool-use loop,
/// thinking-block round-trips, product grounding, refusals and graceful degradation.
/// </summary>
public sealed class ClaudeShoppingAssistantTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync();
    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    private (ClaudeShoppingAssistant Assistant, ScriptedHandler Handler) Create(params HttpResponseMessage[] responses)
    {
        var handler = new ScriptedHandler(responses);
        var client = new AnthropicClient
        {
            ApiKey = "test-key",
            HttpClient = new HttpClient(handler),
            MaxRetries = 0,
        };
        var catalog = new CatalogService(_db.CreateContext(), TestDatabase.Clock);
        var options = Options.Create(new AnthropicOptions { ApiKey = "test-key" });
        var assistant = new ClaudeShoppingAssistant(
            client, catalog, new OfflineShoppingAssistant(catalog), options, NullLogger<ClaudeShoppingAssistant>.Instance);
        return (assistant, handler);
    }

    private static readonly ChatMessageDto[] Conversation =
        [new() { Role = ChatRole.User, Content = "Waterproof shoes for trail running?" }];

    [Fact]
    public async Task Runs_tool_loop_and_returns_grounded_products()
    {
        var (assistant, handler) = Create(
            Json(Message("tool_use",
                new { type = "thinking", thinking = "", signature = "sig-1" },
                new { type = "text", text = "Let me check the catalog." },
                new { type = "tool_use", id = "toolu_1", name = "search_products", input = new { query = "waterproof trail shoes" } })),
            Json(Message("end_turn",
                new { type = "text", text = "The Trail Runner GTX [[product:1]] is waterproof. Also see [[product:999]]." })));

        var reply = await assistant.ReplyAsync(Conversation);

        Assert.Equal("claude", reply.Provider);
        Assert.Equal("The Trail Runner GTX is waterproof. Also see.", reply.Reply);
        var product = Assert.Single(reply.Products); // 999 was never returned by a tool → dropped
        Assert.Equal(1, product.Id);

        Assert.Equal(2, handler.Requests.Count);
        var first = handler.Requests[0];
        Assert.Equal("claude-opus-5-5", first["model"]!.GetValue<string>());
        Assert.Equal("default", first["fallbacks"]!.GetValue<string>());
        Assert.Equal("low", first["output_config"]!["effort"]!.GetValue<string>());
        Assert.Equal(3, first["tools"]!.AsArray().Count);

        // Second request echoes the assistant turn (thinking signature intact) plus one tool_result message.
        var messages = handler.Requests[1]["messages"]!.AsArray();
        Assert.Equal(3, messages.Count);
        var assistantTurn = messages[1]!["content"]!.AsArray();
        Assert.Equal("thinking", assistantTurn[0]!["type"]!.GetValue<string>());
        Assert.Equal("sig-1", assistantTurn[0]!["signature"]!.GetValue<string>());
        Assert.Equal("tool_use", assistantTurn[2]!["type"]!.GetValue<string>());
        var toolResult = messages[2]!["content"]!.AsArray().Single()!;
        Assert.Equal("toolu_1", toolResult["tool_use_id"]!.GetValue<string>());
        Assert.Contains("Trail Runner GTX", toolResult["content"]!.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_tools_return_error_results_instead_of_throwing()
    {
        var (assistant, handler) = Create(
            Json(Message("tool_use", new { type = "tool_use", id = "toolu_x", name = "delete_everything", input = new { } })),
            Json(Message("end_turn", new { type = "text", text = "Sorry, I can only search the catalog." })));

        var reply = await assistant.ReplyAsync(Conversation);

        Assert.Equal("Sorry, I can only search the catalog.", reply.Reply);
        var result = handler.Requests[1]["messages"]!.AsArray()[2]!["content"]!.AsArray().Single()!;
        Assert.True(result["is_error"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Refusals_return_a_polite_message()
    {
        var (assistant, _) = Create(Json(Message("refusal")));

        var reply = await assistant.ReplyAsync(Conversation);

        Assert.StartsWith("Sorry, I can't help with that request.", reply.Reply, StringComparison.Ordinal);
        Assert.Empty(reply.Products);
    }

    [Fact]
    public async Task Api_failures_degrade_to_the_offline_assistant()
    {
        var (assistant, _) = Create(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"type":"error","error":{"type":"api_error","message":"boom"}}""", Encoding.UTF8, "application/json"),
        });

        var reply = await assistant.ReplyAsync(Conversation);

        Assert.Equal("offline", reply.Provider);
        Assert.Contains(reply.Products, p => p.Name == "Trail Runner GTX");
    }

    [Fact]
    public async Task Stops_after_the_tool_iteration_limit()
    {
        var toolCall = () => Json(Message("tool_use",
            new { type = "tool_use", id = $"toolu_{Guid.NewGuid():N}", name = "list_categories", input = new { } }));
        var (assistant, handler) = Create([.. Enumerable.Range(0, 6).Select(_ => toolCall())]);

        var reply = await assistant.ReplyAsync(Conversation);

        Assert.Equal(6, handler.Requests.Count);
        Assert.Contains("tell me a bit more", reply.Reply, StringComparison.Ordinal);
    }

    private static object Message(string stopReason, params object[] content) => new
    {
        id = $"msg_{Guid.NewGuid():N}",
        type = "message",
        role = "assistant",
        model = "claude-opus-5-5",
        content,
        stop_reason = stopReason,
        stop_sequence = (string?)null,
        usage = new { input_tokens = 100, output_tokens = 20 },
    };

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    private sealed class ScriptedHandler(IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<JsonNode> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Add(JsonNode.Parse(body)!);
            return _responses.Count > 0
                ? _responses.Dequeue()
                : throw new InvalidOperationException("No scripted response left.");
        }
    }
}
