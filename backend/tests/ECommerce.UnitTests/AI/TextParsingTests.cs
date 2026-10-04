using ECommerce.Infrastructure.AI;
using ECommerce.Infrastructure.Services;

namespace ECommerce.UnitTests.AI;

public class TextParsingTests
{
    [Fact]
    public void ProductReferences_extracts_ids_in_order_and_strips_markers()
    {
        var (text, ids) = ProductReferences.Extract(
            "Try the Trail Runner GTX [[product:5]] or the Canvas Sneaker [[product:6]]. The GTX [[product:5]] is waterproof.");

        Assert.Equal([5, 6], ids);
        Assert.Equal("Try the Trail Runner GTX or the Canvas Sneaker. The GTX is waterproof.", text);
    }

    [Fact]
    public void ProductReferences_caps_number_of_products()
    {
        var input = string.Join(' ', Enumerable.Range(1, 10).Select(i => $"P{i} [[product:{i}]]"));
        var (_, ids) = ProductReferences.Extract(input, max: 3);
        Assert.Equal([1, 2, 3], ids);
    }

    [Theory]
    [InlineData("gift under 50 euros", 50)]
    [InlineData("something below €79.90", 79.90)]
    [InlineData("headphones max 200€", 200)]
    [InlineData("less than 30,5 eur", 30.5)]
    public void Offline_assistant_parses_price_limits(string message, double expected) =>
        Assert.Equal((decimal)expected, OfflineShoppingAssistant.ParseMaxPrice(message));

    [Fact]
    public void Offline_assistant_returns_null_without_price_limit() =>
        Assert.Null(OfflineShoppingAssistant.ParseMaxPrice("waterproof shoes"));

    [Fact]
    public void Tokenize_drops_stop_words_stems_plurals_and_dedupes()
    {
        var terms = SearchExpressions.Tokenize("I need some waterproof Shoes, for the shoes!");
        Assert.Equal(["waterproof", "shoe"], terms);
    }

    [Fact]
    public void Tokenize_handles_empty_input() => Assert.Empty(SearchExpressions.Tokenize("   "));
}
