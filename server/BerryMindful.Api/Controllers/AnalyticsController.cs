using System.Security.Claims;
using BerryMindful.Services.AnalyticsServices;
using BerryMindful.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BerryMindful.Api.Controllers;

[ApiController]
[Route("analytics")]
[Authorize]
public class AnalyticsController(WasteAnalyticsService analyticsService) : ControllerBase
{
    [HttpGet("waste")]
    public async Task<ActionResult<WasteAnalyticsDto>> GetWaste([FromQuery] int days = 90)
    {
        return Ok(await analyticsService.GetWasteAsync(UserId, days));
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
