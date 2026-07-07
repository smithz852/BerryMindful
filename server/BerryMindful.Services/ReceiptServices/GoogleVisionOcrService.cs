using Google.Api.Gax.Grpc;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Vision.V1;

namespace BerryMindful.Services.ReceiptServices;

// TEXT_DETECTION via Google Cloud Vision. Credentials come from an explicit
// service-account JSON path (GoogleVision:CredentialsPath) or, when unset, the
// standard GOOGLE_APPLICATION_CREDENTIALS environment variable.
public class GoogleVisionOcrService(string? credentialsPath) : IOcrService
{
    // Built lazily so a bad credentials path fails the scan (as a ReceiptScanException
    // upstream) instead of preventing the app from starting.
    private readonly Lazy<ImageAnnotatorClient> _client = new(() =>
    {
        var clientBuilder = new ImageAnnotatorClientBuilder();
        if (!string.IsNullOrWhiteSpace(credentialsPath))
        {
            clientBuilder.GoogleCredential = CredentialFactory
                .FromFile<ServiceAccountCredential>(credentialsPath)
                .ToGoogleCredential();
        }
        return clientBuilder.Build();
    });

    public async Task<string> ExtractTextAsync(Stream image, CancellationToken cancellationToken = default)
    {
        var visionImage = await Image.FromStreamAsync(image);
        var annotations = await _client.Value.DetectTextAsync(
            visionImage,
            callSettings: CallSettings.FromCancellationToken(cancellationToken));

        // The first annotation is the full detected text block; the rest are
        // per-word bounding boxes we don't need.
        return annotations.FirstOrDefault()?.Description ?? string.Empty;
    }
}
