using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    // GET: api/appointments/clinic/{clinicId}
    [HttpGet("clinic/{clinicId:guid}")]
    [Authorize(Policy = "CanManageAppointments")]
    public async Task<ActionResult<List<Appointment>>> GetByClinic(
        Guid clinicId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
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
        return Ok(appointment);
    }

    // POST: api/appointments
    [HttpPost]
    [Authorize(Policy = "CanManageAppointments")]
    public async Task<ActionResult<Appointment>> Create([FromBody] CreateAppointmentDto dto)
    {
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