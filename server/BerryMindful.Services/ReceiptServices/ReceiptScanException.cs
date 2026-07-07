namespace BerryMindful.Services.ReceiptServices;

// Thrown when the OCR or parsing stage of the scan pipeline fails for API-level
// reasons (timeout, rate limit, bad credentials) — the controller maps this to a
// 502 so the client can offer the manual-entry fallback.
public class ReceiptScanException(string message, Exception? innerException = null)
    : Exception(message, innerException);
