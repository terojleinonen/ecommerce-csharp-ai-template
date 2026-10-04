using ECommerce.Core.Catalog;
using ECommerce.Core.Common;

namespace ECommerce.Infrastructure.Data;

/// <summary>Demo catalog used for local development and the hosted demo.</summary>
internal static class SeedData
{
    public static IReadOnlyList<Category> Categories() =>
    [
        new() { Name = "Apparel", Slug = "apparel", Description = "Everyday clothing built to last." },
        new() { Name = "Footwear", Slug = "footwear", Description = "Shoes for trails, streets and everything between." },
        new() { Name = "Electronics", Slug = "electronics", Description = "Audio, peripherals and smart gadgets." },
        new() { Name = "Home & Kitchen", Slug = "home-kitchen", Description = "Thoughtful tools for the home." },
        new() { Name = "Outdoor", Slug = "outdoor", Description = "Gear for camping, hiking and travel." },
    ];

    private sealed record P(string Sku, string Name, string Category, decimal Price, int Stock, string Description);

    private static readonly P[] Products =
    [
        new("APP-TEE-001", "Organic Cotton Tee", "apparel", 24.90m, 120, "Heavyweight 200 gsm organic cotton T-shirt with a relaxed fit. Pre-shrunk, garment-dyed and soft from the first wear."),
        new("APP-HOOD-002", "Merino Zip Hoodie", "apparel", 119.00m, 35, "Lightweight merino wool hoodie that regulates temperature and resists odour. Ideal for travel and cool evenings."),
        new("APP-JKT-003", "Packable Rain Jacket", "apparel", 89.00m, 40, "Waterproof, breathable 2.5-layer shell that packs into its own pocket. Taped seams and adjustable hood."),
        new("APP-SOCK-004", "Wool Hiking Socks (3-pack)", "apparel", 29.90m, 200, "Cushioned merino blend socks with arch support. Warm in winter, breathable in summer."),
        new("FTW-RUN-001", "Trail Runner GTX", "footwear", 149.00m, 25, "Waterproof trail running shoe with aggressive lugs, rock plate and a responsive foam midsole. Great for wet autumn trails."),
        new("FTW-SNK-002", "Everyday Canvas Sneaker", "footwear", 64.90m, 80, "Minimal low-top sneaker in organic canvas with a natural rubber sole. Pairs with anything."),
        new("FTW-BOOT-003", "Insulated Winter Boot", "footwear", 179.00m, 18, "Warm, waterproof boot rated to -30 °C with a grippy winter outsole and removable felt liner."),
        new("ELE-HP-001", "Noise-Cancelling Headphones", "electronics", 249.00m, 30, "Over-ear wireless headphones with adaptive noise cancelling, 40-hour battery life and multipoint Bluetooth."),
        new("ELE-EAR-002", "Sport Wireless Earbuds", "electronics", 99.00m, 60, "Sweat-proof IPX5 earbuds with secure ear hooks, 8-hour playback and a pocketable charging case."),
        new("ELE-MOU-003", "Ergonomic Wireless Mouse", "electronics", 59.90m, 75, "Sculpted vertical mouse that reduces wrist strain. Silent clicks, USB-C charging and 3-device pairing."),
        new("ELE-KEY-004", "Mechanical Keyboard 75%", "electronics", 139.00m, 22, "Hot-swappable 75% keyboard with gasket mount, PBT keycaps and tactile switches. Wired and Bluetooth."),
        new("ELE-SPK-005", "Portable Bluetooth Speaker", "electronics", 79.00m, 45, "Rugged, waterproof speaker with 360° sound and 20 hours of battery. Floats in water."),
        new("HOM-POUR-001", "Pour-Over Coffee Set", "home-kitchen", 49.90m, 50, "Ceramic dripper, glass carafe and 100 paper filters for a clean, bright cup of coffee."),
        new("HOM-KNF-002", "Chef's Knife 20 cm", "home-kitchen", 89.00m, 30, "Forged stainless-steel chef's knife with a full tang and comfortable pakkawood handle."),
        new("HOM-PAN-003", "Cast Iron Skillet 26 cm", "home-kitchen", 54.00m, 40, "Pre-seasoned cast iron skillet for searing, baking and campfire cooking. Lasts a lifetime."),
        new("HOM-LMP-004", "Smart Desk Lamp", "home-kitchen", 69.00m, 3, "Dimmable LED desk lamp with adjustable colour temperature, USB-C charging port and app control."),
        new("OUT-TENT-001", "Ultralight 2-Person Tent", "outdoor", 329.00m, 12, "1.4 kg freestanding tent with two doors, two vestibules and a fully taped rainfly."),
        new("OUT-BAG-002", "Down Sleeping Bag -5 °C", "outdoor", 219.00m, 15, "Responsibly sourced 650-fill down sleeping bag with a comfort rating of -5 °C. Compresses to 3 litres."),
        new("OUT-PACK-003", "Daypack 24L", "outdoor", 74.90m, 55, "Ventilated back panel, hydration sleeve and rain cover. Fits a 15\" laptop for the commute."),
        new("OUT-BTL-004", "Insulated Water Bottle 750 ml", "outdoor", 32.00m, 0, "Double-wall vacuum insulated bottle keeps drinks cold for 24 hours or hot for 12."),
    ];

    public static IReadOnlyList<Product> ProductsFor(IReadOnlyDictionary<string, int> categoryIdsBySlug, DateTimeOffset now) =>
    [
        .. Products.Select((p, index) => new Product
        {
            Sku = p.Sku,
            Name = p.Name,
            Slug = Slug.From(p.Name),
            Description = p.Description,
            Price = p.Price,
            StockQuantity = p.Stock,
            CategoryId = categoryIdsBySlug[p.Category],
            // Stagger timestamps so "newest" sorting is deterministic.
            CreatedAt = now.AddMinutes(-index),
            UpdatedAt = now.AddMinutes(-index),
        }),
    ];
}
