using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Klinara.Web.Authorization;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Klinara.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClinicHolidaysController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ClinicHolidaysController(ApplicationDbContext context)
    {
        _context = context;
    }

    private Guid? GetCurrentUserClinicId()
    {
        var claim = User.FindFirst("ClinicId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private bool IsPlatformAdmin() => User.IsInRole(KlinaraRoles.PlatformAdmin);

    private ActionResult ValidateClinicAccess(Guid clinicId)
    {
        var userClinicId = GetCurrentUserClinicId();
        if (userClinicId.HasValue && clinicId != userClinicId.Value && !IsPlatformAdmin())
            return Forbid();
        return null;
    }

    // GET: api/clinicholidays/clinic/{clinicId}
    [HttpGet("clinic/{clinicId:guid}")]
    [Authorize(Policy = "CanManageHolidays")]
    public async Task<ActionResult<List<ClinicHoliday>>> GetByClinic(
        Guid clinicId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var validation = ValidateClinicAccess(clinicId);
        if (validation != null) return validation;

        var query = _context.ClinicHolidays.Where(h => h.ClinicId == clinicId);

        if (startDate.HasValue) query = query.Where(h => h.Date >= startDate.Value);
        if (endDate.HasValue) query = query.Where(h => h.Date <= endDate.Value);

        var holidays = await query.OrderBy(h => h.Date).ToListAsync();
        return Ok(holidays);
    }

    // POST: api/clinicholidays
    [HttpPost]
    [Authorize(Policy = "CanManageHolidays")]
    public async Task<ActionResult<ClinicHoliday>> Create([FromBody] CreateClinicHolidayDto dto)
    {
        var validation = ValidateClinicAccess(dto.ClinicId);
        if (validation != null) return validation;

        var holiday = new ClinicHoliday
        {
            Id = Guid.NewGuid(),
            ClinicId = dto.ClinicId,
            Name = dto.Name,
            Date = dto.Date,
            IsRecurring = dto.IsRecurring,
            IsFullDay = dto.IsFullDay,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            CreatedAt = DateTime.UtcNow
        };

        _context.ClinicHolidays.Add(holiday);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByClinic), new { clinicId = holiday.ClinicId }, holiday);
    }

    // DELETE: api/clinicholidays/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "CanManageHolidays")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var holiday = await _context.ClinicHolidays.FindAsync(id);
        if (holiday == null) return NotFound();

        var validation = ValidateClinicAccess(holiday.ClinicId);
        if (validation != null) return validation;

        _context.ClinicHolidays.Remove(holiday);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateClinicHolidayDto(
    Guid ClinicId,
    string Name,
    DateTime Date,
    bool IsRecurring,
    bool IsFullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime
);