using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Klinara.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DoctorVacationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DoctorVacationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/doctorvacations/clinic/{clinicId}/doctor/{doctorId}
    [HttpGet("clinic/{clinicId:guid}/doctor/{doctorId:guid}")]
    [Authorize(Policy = "CanManageVacations")]
    public async Task<ActionResult<List<DoctorVacation>>> GetByDoctor(
        Guid clinicId,
        Guid doctorId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var query = _context.DoctorVacations
            .Where(v => v.ClinicId == clinicId && v.DoctorId == doctorId);

        if (startDate.HasValue) query = query.Where(v => v.EndDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(v => v.StartDate <= endDate.Value);

        var vacations = await query.OrderBy(v => v.StartDate).ToListAsync();
        return Ok(vacations);
    }

    // POST: api/doctorvacations
    [HttpPost]
    [Authorize(Policy = "CanManageVacations")]
    public async Task<ActionResult<DoctorVacation>> Create([FromBody] CreateDoctorVacationDto dto)
    {
        var vacation = new DoctorVacation
        {
            Id = Guid.NewGuid(),
            ClinicId = dto.ClinicId,
            DoctorId = dto.DoctorId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Reason = dto.Reason,
            CreatedAt = DateTime.UtcNow
        };

        _context.DoctorVacations.Add(vacation);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByDoctor), new { clinicId = vacation.ClinicId, doctorId = vacation.DoctorId }, vacation);
    }

    // DELETE: api/doctorvacations/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "CanManageVacations")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var vacation = await _context.DoctorVacations.FindAsync(id);
        if (vacation == null) return NotFound();

        _context.DoctorVacations.Remove(vacation);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateDoctorVacationDto(
    Guid ClinicId,
    Guid DoctorId,
    DateTime StartDate,
    DateTime EndDate,
    string? Reason
);