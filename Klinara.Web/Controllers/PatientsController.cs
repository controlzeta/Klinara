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
public class PatientsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PatientsController(ApplicationDbContext context)
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

    // GET: api/patients/clinic/{clinicId}
    [HttpGet("clinic/{clinicId:guid}")]
    [Authorize(Policy = "CanManagePatients")]
    public async Task<ActionResult<List<Patient>>> GetByClinic(Guid clinicId)
    {
        var validation = ValidateClinicAccess(clinicId);
        if (validation != null) return validation;

        var patients = await _context.Patients
            .Where(p => p.ClinicId == clinicId)
            .OrderBy(p => p.FullName)
            .ToListAsync();
        return Ok(patients);
    }

    // GET: api/patients/{id}
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "CanManagePatients")]
    public async Task<ActionResult<Patient>> GetById(Guid id)
    {
        var patient = await _context.Patients
            .Include(p => p.Appointments)
            .Include(p => p.Tokens)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null) return NotFound();

        var validation = ValidateClinicAccess(patient.ClinicId);
        if (validation != null) return validation;

        return Ok(patient);
    }

    // POST: api/patients
    [HttpPost]
    [Authorize(Policy = "CanManagePatients")]
    public async Task<ActionResult<Patient>> Create([FromBody] CreatePatientDto dto)
    {
        var validation = ValidateClinicAccess(dto.ClinicId);
        if (validation != null) return validation;

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            ClinicId = dto.ClinicId,
            FullName = dto.FullName,
            Email = dto.Email,
            Phone = dto.Phone,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            Address = dto.Address,
            Notes = dto.Notes,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
    }

    // PUT: api/patients/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "CanManagePatients")]
    public async Task<ActionResult<Patient>> Update(Guid id, [FromBody] UpdatePatientDto dto)
    {
        var patient = await _context.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        var validation = ValidateClinicAccess(patient.ClinicId);
        if (validation != null) return validation;

        patient.FullName = dto.FullName;
        patient.Email = dto.Email;
        patient.Phone = dto.Phone;
        patient.DateOfBirth = dto.DateOfBirth;
        patient.Gender = dto.Gender;
        patient.Address = dto.Address;
        patient.Notes = dto.Notes;
        patient.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(patient);
    }
}

public record CreatePatientDto(
    Guid ClinicId,
    string FullName,
    string? Email,
    string? Phone,
    DateTime? DateOfBirth,
    string? Gender,
    string? Address,
    string? Notes,
    Guid CreatedByUserId
);

public record UpdatePatientDto(
    string FullName,
    string? Email,
    string? Phone,
    DateTime? DateOfBirth,
    string? Gender,
    string? Address,
    string? Notes
);