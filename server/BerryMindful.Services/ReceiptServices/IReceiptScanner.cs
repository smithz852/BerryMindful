using BerryMindful.Services.DTOs;

namespace BerryMindful.Services.ReceiptServices;

// OCR + item-extraction pipeline behind one seam: the stub returns canned items now;
// the Vision + Claude implementation replaces it without touching controllers.
public interface IReceiptScanner
{
    Task<ReceiptScanResultDto> ScanAsync(Stream image, string fileName, CancellationToken cancellationToken = default);
}
