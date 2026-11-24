using System;
using System.Threading.Tasks;
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

    // POST: api/slotgenerator/generate
    [HttpPost("generate")]
    [Authorize(Policy = "CanManageSchedules")]
    public async Task<ActionResult> GenerateSlots()
    {
        await _slotGenerator.GenerateSlotsAsync();
        return Ok(new { Success = true, Message = "Generación de slots completada" });
    }
}