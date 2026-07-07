using System.Security.Claims;
using BerryMindful.Services.DTOs;
using BerryMindful.Services.PantryServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BerryMindful.Api.Controllers;

[ApiController]
[Route("pantry")]
[Authorize]
public class PantryController(PantryService pantryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PantryItemDto>>> GetActive()
    {
        return Ok(await pantryService.GetActiveAsync(UserId));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<PantryItemDto>> UpdateStatus(Guid id, UpdateStatusRequest request)
    {
        var updated = await pantryService.UpdateStatusAsync(UserId, id, request.Status);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("items")]
    public async Task<ActionResult<PantryItemDto>> AddItem(AddPantryItemRequest request)
    {
        var item = await pantryService.AddManualAsync(UserId, request);
        return CreatedAtAction(nameof(GetActive), item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await pantryService.DeleteAsync(UserId, id);
        return deleted ? NoContent() : NotFound();
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
