using System.Security.Claims;
using BerryMindful.Services.DTOs;
using BerryMindful.Services.ReceiptServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BerryMindful.Api.Controllers;

[ApiController]
[Route("receipts")]
[Authorize]
public class ReceiptsController(
    IReceiptScanner scanner,
    ReceiptService receiptService,
    IWebHostEnvironment env,
    ILogger<ReceiptsController> logger) : ControllerBase
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxImageBytes = 10 * 1024 * 1024;

    [HttpPost("scan")]
    [EnableRateLimiting("scan")]
    [RequestSizeLimit(MaxImageBytes)]
    public async Task<ActionResult<ReceiptScanResultDto>> Scan(IFormFile image, CancellationToken cancellationToken)
    {
        if (image.Length == 0)
        {
            return BadRequest(new { error = "Empty image upload." });
        }
        if (!AllowedContentTypes.Contains(image.ContentType))
        {
            return BadRequest(new { error = "Only JPEG, PNG, or WebP images are accepted." });
        }

        var uploadsDir = Path.Combine(env.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);
        var extension = image.ContentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg",
        };
        var storedName = $"{Guid.NewGuid()}{extension}";
        var storedPath = Path.Combine(uploadsDir, storedName);

        await using (var fileStream = System.IO.File.Create(storedPath))
        {
            await image.CopyToAsync(fileStream, cancellationToken);
        }

        await using var scanStream = System.IO.File.OpenRead(storedPath);
        try
        {
            var result = await scanner.ScanAsync(scanStream, storedName, cancellationToken);
            return Ok(result with { ImageUrl = $"/uploads/{storedName}" });
        }
        catch (ReceiptScanException ex)
        {
            logger.LogError(ex, "Receipt scan pipeline failed for {StoredName}", storedName);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "We couldn't read that receipt right now. Try again, or add items manually.",
            });
        }
    }

    [HttpPost("confirm")]
    public async Task<ActionResult<List<PantryItemDto>>> Confirm(ConfirmReceiptRequest request)
    {
        var items = await receiptService.ConfirmAsync(UserId, request);
        return Ok(items);
    }

    [HttpGet]
    public async Task<ActionResult<List<ReceiptSummaryDto>>> List()
    {
        return Ok(await receiptService.ListAsync(UserId));
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
