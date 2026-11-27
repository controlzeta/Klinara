using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Klinara.Web.Authorization;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Klinara.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;
    private readonly ApplicationDbContext _context;

    public AppointmentsController(IAppointmentService appointmentService, ApplicationDbContext context)
    {
        _appointmentService = appointmentService;
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

    private async Task<ActionResult> ValidateResourceClinicAccessAsync(Guid resourceId, Func<Guid, Task<Guid?>> getClinicId)
    {
        var resourceClinicId = await getClinicId(resourceId);
        if (!resourceClinicId.HasValue)
            return NotFound();

        var validation = ValidateClinicAccess(resourceClinicId.Value);
        return validation;
    }

    // GET: api/appointments/clinic/{clinicId}
    [HttpGet("clinic/{clinicId:guid}")]
    [Authorize(Policy = "CanManageAppointments")]
    public async Task<ActionResult<List<Appointment>>> GetByClinic(
        Guid clinicId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var validation = ValidateClinicAccess(clinicId);
        if (validation != null) return validation;

        var appointments = await _appointmentService.GetAppointmentsByClinicAsync(clinicId, startDate, endDate);
        return Ok(appointments);
    }

    // GET: api/appointments/doctor/{doctorId}
    [HttpGet("doctor/{doctorId:guid}")]
    [Authorize(Policy = "CanManageAppointments")]
    public async Task<ActionResult<List<Appointment>>> GetByDoctor(
        Guid doctorId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var doctor = await _context.Users.FindAsync(doctorId);
        if (doctor == null) return NotFound();

        var validation = ValidateClinicAccess(doctor.ClinicId ?? Guid.Empty);
        if (validation != null) return validation;

        var appointments = await _appointmentService.GetAppointmentsByDoctorAsync(doctorId, startDate, endDate);
        return Ok(appointments);
    }

    // GET: api/appointments/patient/{patientId}
    [HttpGet("patient/{patientId:guid}")]
    [Authorize(Policy = "CanManagePatients")]
    public async Task<ActionResult<List<Appointment>>> GetByPatient(
        Guid patientId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var patient = await _context.Patients.FindAsync(patientId);
        if (patient == null) return NotFound();

        var validation = ValidateClinicAccess(patient.ClinicId);
        if (validation != null) return validation;

        var appointments = await _appointmentService.GetAppointmentsByPatientAsync(patientId, startDate, endDate);
        return Ok(appointments);
    }

    // GET: api/appointments/{id}
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "CanManageAppointments")]
    public async Task<ActionResult<Appointment>> GetById(Guid id)
    {
        var appointment = await _appointmentService.GetAppointmentAsync(id);
        if (appointment == null) return NotFound();

        var validation = ValidateClinicAccess(appointment.ClinicId);
        if (validation != null) return validation;

        return Ok(appointment);
    }

    // POST: api/appointments
    [HttpPost]
    [Authorize(Policy = "CanManageAppointments")]
    public async Task<ActionResult<Appointment>> Create([FromBody] CreateAppointmentDto dto)
    {
        var validation = ValidateClinicAccess(dto.ClinicId);
        if (validation != null) return validation;

        // Verify doctor and patient belong to the clinic
        var doctor = await _context.Users.FindAsync(dto.DoctorId);
        if (doctor == null || doctor.ClinicId != dto.ClinicId)
            return BadRequest("El doctor no pertenece a esta clínica");

        var patient = await _context.Patients.FindAsync(dto.PatientId);
        if (patient == null || patient.ClinicId != dto.ClinicId)
            return BadRequest("El paciente no pertenece a esta clínica");

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = dto.ClinicId,
            DoctorId = dto.DoctorId,
            PatientId = dto.PatientId,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt,
            Type = dto.Type,
            Notes = dto.Notes,
            CreatedByUserId = dto.CreatedByUserId,
            Status = AppointmentStatus.Scheduled
        };

        var result = await _appointmentService.CreateAppointmentAsync(appointment, dto.IsPatientBooking);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return CreatedAtAction(nameof(GetById), new { id = result.Appointment!.Id }, result.Appointment);
    }

    // PUT: api/appointments/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "CanManageAppointments")]
    public async Task<ActionResult<Appointment>> Update(Guid id, [FromBody] UpdateAppointmentDto dto)
    {
        var existing = await _context.Appointments.FindAsync(id);
        if (existing == null) return NotFound();

        var validation = ValidateClinicAccess(existing.ClinicId);
        if (validation != null) return validation;

        // Verify doctor and patient belong to the clinic
        var doctor = await _context.Users.FindAsync(dto.DoctorId);
        if (doctor == null || doctor.ClinicId != existing.ClinicId)
            return BadRequest("El doctor no pertenece a esta clínica");

        var patient = await _context.Patients.FindAsync(dto.PatientId);
        if (patient == null || patient.ClinicId != existing.ClinicId)
            return BadRequest("El paciente no pertenece a esta clínica");

        var appointment = new Appointment
        {
            Id = id,
            ClinicId = dto.ClinicId,
            DoctorId = dto.DoctorId,
            PatientId = dto.PatientId,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt,
            Type = dto.Type,
            Notes = dto.Notes,
            CreatedByUserId = dto.CreatedByUserId
        };

        var result = await _appointmentService.UpdateAppointmentAsync(appointment, dto.IsPatientBooking);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return Ok(result.Appointment);
    }

    // POST: api/appointments/{id}/cancel
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "CanManageAppointments")]
    public async Task<ActionResult<Appointment>> Cancel(Guid id, [FromBody] CancelAppointmentDto dto)
    {
        var validation = await ValidateResourceClinicAccessAsync(id, async (appointmentId) =>
        {
            var appt = await _context.Appointments.FindAsync(appointmentId);
            return appt?.ClinicId;
        });
        if (validation != null) return validation;

        var result = await _appointmentService.CancelAppointmentAsync(id, dto.CancelledByUserId, dto.Reason, dto.IsPatientCancellation);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return Ok(result.Appointment);
    }

    // POST: api/appointments/{id}/noshow
    [HttpPost("{id:guid}/noshow")]
    [Authorize(Policy = "CanManageAppointments")]
    public async Task<ActionResult<Appointment>> MarkNoShow(Guid id, [FromBody] MarkNoShowDto dto)
    {
        var validation = await ValidateResourceClinicAccessAsync(id, async (appointmentId) =>
        {
            var appt = await _context.Appointments.FindAsync(appointmentId);
            return appt?.ClinicId;
        });
        if (validation != null) return validation;

        var result = await _appointmentService.MarkNoShowAsync(id, dto.MarkedByUserId);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return Ok(result.Appointment);
    }

    // POST: api/appointments/patients/{patientId}/unblock
    [HttpPost("patients/{patientId:guid}/unblock")]
    [Authorize(Policy = "CanUnblockPatients")]
    public async Task<ActionResult> UnblockPatient(Guid patientId, [FromBody] UnblockPatientDto dto)
    {
        var patient = await _context.Patients.FindAsync(patientId);
        if (patient == null) return NotFound();

        var validation = ValidateClinicAccess(patient.ClinicId);
        if (validation != null) return validation;

        var result = await _appointmentService.UnblockPatientAsync(patientId, dto.UnblockedByUserId);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return Ok(new { Success = true });
    }
}

// DTOs
public record CreateAppointmentDto(
    Guid ClinicId,
    Guid DoctorId,
    Guid PatientId,
    DateTime StartAt,
    DateTime EndAt,
    AppointmentType Type,
    string? Notes,
    Guid CreatedByUserId,
    bool IsPatientBooking = false
);

public record UpdateAppointmentDto(
    Guid ClinicId,
    Guid DoctorId,
    Guid PatientId,
    DateTime StartAt,
    DateTime EndAt,
    AppointmentType Type,
    string? Notes,
    Guid CreatedByUserId,
    bool IsPatientBooking = false
);

public record CancelAppointmentDto(
    Guid CancelledByUserId,
    string Reason,
    bool IsPatientCancellation = false
);

public record MarkNoShowDto(
    Guid MarkedByUserId
);

public record UnblockPatientDto(
    Guid UnblockedByUserId
);