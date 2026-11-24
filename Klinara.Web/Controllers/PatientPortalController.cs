using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klinara.Web.Controllers;

[ApiController]
[Route("api/patient-portal")]
[AllowAnonymous]
public class PatientPortalController : ControllerBase
{
    private readonly IPatientPortalService _portalService;
    private readonly ILogger<PatientPortalController> _logger;

    public PatientPortalController(IPatientPortalService portalService, ILogger<PatientPortalController> logger)
    {
        _portalService = portalService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene los doctores disponibles para una clínica (público)
    /// </summary>
    [HttpGet("clinics/{subdomain}/doctors")]
    public async Task<ActionResult<List<DoctorAvailabilityDto>>> GetDoctors(string subdomain)
    {
        try
        {
            var doctors = await _portalService.GetAvailableDoctorsAsync(subdomain);
            return Ok(doctors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo doctores para subdominio {Subdomain}", subdomain);
            return StatusCode(500, new { Error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene slots disponibles para un doctor en un rango de fechas (público)
    /// </summary>
    [HttpGet("clinics/{subdomain}/doctors/{doctorId:guid}/slots")]
    public async Task<ActionResult<List<TimeSlot>>> GetSlots(
        string subdomain,
        Guid doctorId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            if (endDate < startDate || (endDate - startDate).TotalDays > 90)
                return BadRequest(new { Error = "Rango de fechas inválido (máx 90 días)" });

            var slots = await _portalService.GetAvailableSlotsAsync(subdomain, doctorId, startDate, endDate);
            return Ok(slots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo slots para doctor {DoctorId} en subdominio {Subdomain}", doctorId, subdomain);
            return StatusCode(500, new { Error = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Envía código OTP de 6 dígitos al email del paciente para verificación
    /// </summary>
    [HttpPost("verify-email")]
    public async Task<ActionResult<PortalVerificationResult>> SendVerificationEmail([FromBody] VerifyEmailDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.ClinicSubdomain))
            return BadRequest(new { Error = "Email y subdominio son requeridos" });

        var result = await _portalService.SendVerificationEmailAsync(dto.Email.Trim(), dto.ClinicSubdomain.Trim());

        if (!result.Success)
            return BadRequest(new { Error = result.Error });

        return Ok(result);
    }

    /// <summary>
    /// Valida el OTP y genera un token PortalAccess (30 min, single-use)
    /// </summary>
    [HttpPost("validate-otp")]
    public async Task<ActionResult<PortalTokenResult>> ValidateOtp([FromBody] ValidateOtpDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.ClinicSubdomain))
            return BadRequest(new { Error = "Email, código y subdominio son requeridos" });

        if (dto.Code.Length != 6 || !dto.Code.All(char.IsDigit))
            return BadRequest(new { Error = "El código debe ser de 6 dígitos" });

        var result = await _portalService.ValidateOtpAndCreatePortalTokenAsync(
            dto.Email.Trim(), dto.Code.Trim(), dto.ClinicSubdomain.Trim());

        if (!result.Success)
            return BadRequest(new { Error = result.Error });

        return Ok(result);
    }

    /// <summary>
    /// Reserva una cita usando el token PortalAccess
    /// </summary>
    [HttpPost("book")]
    public async Task<ActionResult<PortalBookingResult>> BookAppointment([FromBody] BookAppointmentRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PortalToken))
            return BadRequest(new { Error = "Token de acceso es requerido" });

        if (dto.DoctorId == Guid.Empty)
            return BadRequest(new { Error = "Doctor es requerido" });

        if (dto.StartAt >= dto.EndAt)
            return BadRequest(new { Error = "La hora de inicio debe ser anterior a la de fin" });

        var bookDto = new BookAppointmentDto(
            dto.DoctorId,
            dto.StartAt,
            dto.EndAt,
            dto.Type,
            dto.Notes
        );

        var result = await _portalService.BookAppointmentAsync(dto.PortalToken.Trim(), bookDto);

        if (!result.Success)
            return BadRequest(new { Error = result.Error });

        return Ok(result);
    }

    /// <summary>
    /// Confirma una cita usando el token de confirmación (enviado por email)
    /// </summary>
    [HttpGet("confirm/{token}")]
    public async Task<ActionResult<PortalConfirmationResult>> ConfirmAppointment(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { Error = "Token de confirmación es requerido" });

        var result = await _portalService.ConfirmAppointmentAsync(token.Trim());

        if (!result.Success)
            return BadRequest(new { Error = result.Error });

        return Ok(result);
    }

    /// <summary>
    /// Obtiene la vista de una cita (read-only) usando token
    /// </summary>
    [HttpGet("appointments/{token}")]
    public async Task<ActionResult<PortalAppointmentView>> GetAppointment(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { Error = "Token es requerido" });

        var appointment = await _portalService.GetAppointmentViewAsync(token.Trim());

        if (appointment == null)
            return NotFound(new { Error = "Cita no encontrada o token inválido" });

        return Ok(appointment);
    }

    /// <summary>
    /// Cancela una cita usando token PortalAccess (aplica regla <24h → strike)
    /// </summary>
    [HttpPost("cancel/{token}")]
    public async Task<ActionResult<PortalCancellationResult>> CancelAppointment(string token, [FromBody] PortalCancelAppointmentDto dto)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { Error = "Token de acceso es requerido" });

        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { Error = "Motivo de cancelación es requerido" });

        var result = await _portalService.CancelAppointmentAsync(token.Trim(), dto.Reason.Trim());

        if (!result.Success)
            return BadRequest(new { Error = result.Error });

        return Ok(result);
    }
}

// DTOs
public record VerifyEmailDto(string Email, string ClinicSubdomain);
public record ValidateOtpDto(string Email, string Code, string ClinicSubdomain);
public record BookAppointmentRequestDto(
    string PortalToken,
    Guid DoctorId,
    DateTime StartAt,
    DateTime EndAt,
    AppointmentType Type,
    string? Notes
);
public record PortalCancelAppointmentDto(string Reason);