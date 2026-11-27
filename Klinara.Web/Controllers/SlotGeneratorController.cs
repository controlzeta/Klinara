using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Klinara.Web.Authorization;
using Klinara.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klinara.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SlotGeneratorController : ControllerBase
{
    private readonly SlotGeneratorService _slotGenerator;

    public SlotGeneratorController(SlotGeneratorService slotGenerator)
    {
        _slotGenerator = slotGenerator;
    }

    private Guid? GetCurrentUserClinicId()
    {
        var claim = User.FindFirst("ClinicId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private bool IsPlatformAdmin() => User.IsInRole(KlinaraRoles.PlatformAdmin);

    // POST: api/slotgenerator/generate
    [HttpPost("generate")]
    [Authorize(Policy = "CanManageSchedules")]
    public async Task<ActionResult> GenerateSlots()
    {
        var userClinicId = GetCurrentUserClinicId();
        if (userClinicId.HasValue && !IsPlatformAdmin())
        {
            // For non-platform admins, the background service runs for all clinics
            // But we still validate they can only trigger it for their own clinic context
            // This endpoint triggers global generation, so only PlatformAdmin should call it
            return Forbid();
        }

        await _slotGenerator.GenerateSlotsAsync();
        return Ok(new { Success = true, Message = "Generación de slots completada" });
    }
}