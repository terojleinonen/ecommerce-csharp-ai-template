using ECommerce.Core.Common;

namespace ECommerce.UnitTests.Domain;

public class SlugTests
{
    [Theory]
    [InlineData("Trail Runner GTX", "trail-runner-gtx")]
    [InlineData("  Down Sleeping Bag -5 °C ", "down-sleeping-bag-5-c")]
    [InlineData("Chef's Knife 20 cm", "chef-s-knife-20-cm")]
    [InlineData("Crème Brûlée Torch!", "creme-brulee-torch")]
    [InlineData("---", "")]
    public void From_creates_url_friendly_slugs(string input, string expected) =>
        Assert.Equal(expected, Slug.From(input));
}
