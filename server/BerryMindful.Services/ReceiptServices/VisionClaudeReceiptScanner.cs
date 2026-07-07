using BerryMindful.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace BerryMindful.Services.ReceiptServices;

// The real scan pipeline: Google Cloud Vision OCR → Claude item extraction.
// Registered instead of StubReceiptScanner when both keys are configured.
public class VisionClaudeReceiptScanner(
    IOcrService ocr,
    IReceiptParser parser,
    ILogger<VisionClaudeReceiptScanner> logger) : IReceiptScanner
{
    // Receipts are short; anything past this is almost certainly OCR noise from a
    // non-receipt photo, and it caps Claude input tokens.
    private const int MaxOcrChars = 8000;
    private const int MaxStoreNameChars = 128;

    public async Task<ReceiptScanResultDto> ScanAsync(
        Stream image, string fileName, CancellationToken cancellationToken = default)
    {
        string ocrText;
        try
        {
            ocrText = await ocr.ExtractTextAsync(image, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Vision OCR failed for {FileName}", fileName);
            throw new ReceiptScanException("Couldn't read text from the image.", ex);
        }

        if (string.IsNullOrWhiteSpace(ocrText))
        {
            // Not an error — the photo just has no readable text. Return an empty
            // draft so the client shows "no items" and offers manual entry.
            return new ReceiptScanResultDto(null, DateTime.UtcNow.Date, null, ocrText, []);
        }

        if (ocrText.Length > MaxOcrChars)
        {
            ocrText = ocrText[..MaxOcrChars];
        }

        ReceiptParseResult parsed;
        try
        {
            parsed = await parser.ParseAsync(ocrText, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Claude item extraction failed for {FileName}", fileName);
            throw new ReceiptScanException("Couldn't extract items from the receipt.", ex);
        }

        // Use the receipt's printed transaction date when it's plausible (not in the
        // future, not over a year old — OCR misreads happen); otherwise assume today.
        var today = DateTime.UtcNow.Date;
        var purchasedAt = parsed.PurchasedAt is { } date && date <= today && date >= today.AddYears(-1)
            ? date
            : today;

        // MVP heuristic: receipts print the store name on the first line. Phase 2
        // upgrades this to real store detection with shelf-life adjustments.
        var storeName = ocrText.Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);
        if (storeName?.Length > MaxStoreNameChars)
        {
            storeName = storeName[..MaxStoreNameChars];
        }

        return new ReceiptScanResultDto(storeName, purchasedAt, null, ocrText, parsed.Items);
    }
}
