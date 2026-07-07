namespace BerryMindful.Services.ReceiptServices;

public interface IOcrService
{
    Task<string> ExtractTextAsync(Stream image, CancellationToken cancellationToken = default);
}
