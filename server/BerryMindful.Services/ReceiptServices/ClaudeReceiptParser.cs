using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Helpers;
using Anthropic.Models.Messages;
using BerryMindful.Data.Entities;
using BerryMindful.Services.DTOs;

namespace BerryMindful.Services.ReceiptServices;

public interface IReceiptParser
{
    Task<IReadOnlyList<PantryItemDraftDto>> ParseItemsAsync(string ocrText, CancellationToken cancellationToken = default);
}

// Extracts food items from raw receipt OCR text with claude-haiku-4-5 using
// structured outputs — the API guarantees the response matches the ParsedReceipt
// schema, so there is no malformed-JSON fallback path.
public class ClaudeReceiptParser(string apiKey) : IReceiptParser
{
    private const string SystemPrompt =
        """
        You are a grocery item identifier. Given raw receipt OCR text, extract each
        food item. For each item, provide:
        - name: human-readable food name
        - category: the closest matching category
        - estimatedExpiryDays: integer, typical days until spoilage from purchase date
          (assume proper refrigeration/storage). Use conservative estimates.

        Common abbreviations: ORG/ORGC = Organic, BNNA = Banana, STRBRY = Strawberry,
        MLK = Milk, CHKN = Chicken, WHL = Whole, LF = Low Fat, 3PK/2PK = multipack (ignore count),
        SML/LRG = size descriptor (ignore), DELI = deli department, BF = Beef, GND = Ground,
        YGT = Yogurt, CHDR = Cheddar, BROC = Broccoli, AVCD = Avocado, TMAT = Tomato.

        Skip non-food lines: tax, subtotal, total, store name, cashier, loyalty points, coupon.

        Examples:

        Input: "ORG BNNA 3PK  1.49"
        Output: {"items":[{"name":"Organic Bananas","category":"produce","estimatedExpiryDays":5}]}

        Input: "GND BF 93/7 LB  5.99\nWHL MLK GAL  3.49\nORG STRBRY PINT  4.99"
        Output: {"items":[{"name":"Ground Beef 93/7","category":"meat","estimatedExpiryDays":2},{"name":"Whole Milk","category":"dairy","estimatedExpiryDays":10},{"name":"Organic Strawberries","category":"produce","estimatedExpiryDays":4}]}
        """;

    private readonly AnthropicClient _client = new() { ApiKey = apiKey };

    public async Task<IReadOnlyList<PantryItemDraftDto>> ParseItemsAsync(
        string ocrText, CancellationToken cancellationToken = default)
    {
        var message = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 4000,
            System = SystemPrompt,
            OutputConfig = new OutputConfig
            {
                Format = StructuredOutput.CreateJsonFormat<ParsedReceipt>(),
            },
            Messages = [new MessageParam { Role = Role.User, Content = ocrText }],
        }, cancellationToken);

        var json = string.Concat(message.Content
            .Select(block => block.TryPickText(out var text) ? text.Text : string.Empty));
        var parsed = StructuredOutput.Parse<ParsedReceipt>(json);

        return parsed.Items
            .Select(item => new PantryItemDraftDto(
                item.Name,
                Enum.TryParse<ItemCategory>(item.Category, ignoreCase: true, out var category)
                    ? category
                    : ItemCategory.Other,
                Math.Clamp(item.EstimatedExpiryDays, 0, 3650)))
            .ToList();
    }
}

// Structured-output shape — StructuredOutput.CreateJsonFormat<T>() generates the
// JSON schema from these types, and the API enforces it on the response.
public sealed class ParsedReceipt
{
    [JsonPropertyName("items")]
    public List<ParsedReceiptItem> Items { get; set; } = [];
}

public sealed class ParsedReceiptItem
{
    [JsonPropertyName("name")]
    [SchemaProperty("Human-readable food item name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    [SchemaProperty("Closest matching category",
        Enum = ["produce", "dairy", "meat", "seafood", "bakery", "frozen", "pantry", "beverage", "other"])]
    public string Category { get; set; } = "other";

    [JsonPropertyName("estimatedExpiryDays")]
    [SchemaProperty("Typical days until spoilage from purchase date, assuming proper storage; conservative",
        Minimum = 0, Maximum = 3650)]
    public int EstimatedExpiryDays { get; set; }
}
