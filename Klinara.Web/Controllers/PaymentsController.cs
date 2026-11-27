using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klinara.Web.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize(Policy = "EmployeeOrAbove")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IAdminExportService _exportService;

    public PaymentsController(IPaymentService paymentService, IAdminExportService exportService)
    {
        _paymentService = paymentService;
        _exportService = exportService;
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResult>> RegisterPayment([FromBody] RegisterPaymentRequestDto req)
    {
        var userId = GetCurrentUserId();
        var userClinicId = GetCurrentUserClinicId();
        
        // Validar que el clinicId del request coincide con la clínica del usuario (excepto PlatformAdmin)
        if (userClinicId.HasValue && req.ClinicId != userClinicId.Value)
            return Forbid();

        var dto = new RegisterPaymentDto(
            req.ClinicId,
            req.PatientId,
            req.AppointmentId,
            req.Amount,
            req.Method,
            req.Concept,
            req.Notes,
            req.PaidAt
        );

        var result = await _paymentService.RegisterPaymentAsync(dto, userId);
        if (!result.Success)
            return BadRequest(new { Error = result.Error });

        return Ok(result);
    }

    [HttpGet("patients/{patientId:guid}/balance")]
    public async Task<ActionResult<PatientBalanceDto>> GetPatientBalance(Guid patientId)
    {
        try
        {
            var userClinicId = GetCurrentUserClinicId();
            var balance = await _paymentService.GetPatientBalanceAsync(patientId, userClinicId);
            return Ok(balance);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Error = "Paciente no encontrado" });
        }
    }

    [HttpGet("clinics/{clinicId:guid}")]
    public async Task<ActionResult<List<PaymentSummaryDto>>> GetClinicPayments(
        Guid clinicId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userClinicId = GetCurrentUserClinicId();
        
        // PlatformAdmin puede ver cualquier clínica; ClinicAdmin/Doctor/Employee solo la suya
        if (userClinicId.HasValue && clinicId != userClinicId.Value)
            return Forbid();

        var payments = await _paymentService.GetPaymentsByClinicAsync(clinicId, startDate, endDate);
        return Ok(payments);
    }

    [HttpPut("patients/{patientId:guid}/total-due")]
    public async Task<ActionResult> UpdateTotalDue(Guid patientId, [FromBody] UpdateTotalDueDto req)
    {
        var userClinicId = GetCurrentUserClinicId();
        
        var result = await _paymentService.UpdatePatientTotalDueAsync(patientId, req.TotalDue, userClinicId);
        if (!result.Success)
            return BadRequest(new { Error = result.Error });

        return Ok(new { Success = true });
    }

    // ===== EXPORT ENDPOINTS =====

    [HttpGet("clinics/{clinicId:guid}/export")]
    [Authorize(Policy = "CanManagePayments")]
    public async Task<ActionResult> ExportPayments(
        Guid clinicId,
        [FromQuery] string format = "excel",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? patientId = null)
    {
        var userClinicId = GetCurrentUserClinicId();
        if (userClinicId.HasValue && clinicId != userClinicId.Value)
            return Forbid();

        try
        {
            byte[] fileBytes;
            string contentType;
            string fileName;

            switch (format.ToLower())
            {
                case "csv":
                    fileBytes = await _exportService.ExportPaymentsCsvAsync(clinicId, startDate, endDate, patientId);
                    contentType = "text/csv";
                    fileName = $"pagos_{clinicId}_{DateTime.Now:yyyyMMdd}.csv";
                    break;
                case "pdf":
                    fileBytes = await _exportService.ExportPaymentsPdfAsync(clinicId, startDate, endDate, patientId);
                    contentType = "application/pdf";
                    fileName = $"pagos_{clinicId}_{DateTime.Now:yyyyMMdd}.pdf";
                    break;
                case "excel":
                case "xlsx":
                default:
                    fileBytes = await _exportService.ExportPaymentsExcelAsync(clinicId, startDate, endDate, patientId);
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    fileName = $"pagos_{clinicId}_{DateTime.Now:yyyyMMdd}.xlsx";
                    break;
            }

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = $"Error generando reporte: {ex.Message}" });
        }
    }

    [HttpGet("clinics/{clinicId:guid}/balance/export")]
    [Authorize(Policy = "CanManagePayments")]
    public async Task<ActionResult> ExportPatientBalances(
        Guid clinicId,
        [FromQuery] string format = "excel")
    {
        var userClinicId = GetCurrentUserClinicId();
        if (userClinicId.HasValue && clinicId != userClinicId.Value)
            return Forbid();

        try
        {
            byte[] fileBytes;
            string contentType;
            string fileName;

            switch (format.ToLower())
            {
                case "csv":
                    fileBytes = await _exportService.ExportPatientBalanceCsvAsync(clinicId);
                    contentType = "text/csv";
                    fileName = $"saldos_pacientes_{clinicId}_{DateTime.Now:yyyyMMdd}.csv";
                    break;
                case "pdf":
                    fileBytes = await _exportService.ExportPatientBalancePdfAsync(clinicId);
                    contentType = "application/pdf";
                    fileName = $"saldos_pacientes_{clinicId}_{DateTime.Now:yyyyMMdd}.pdf";
                    break;
                case "excel":
                case "xlsx":
                default:
                    fileBytes = await _exportService.ExportPatientBalanceExcelAsync(clinicId);
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    fileName = $"saldos_pacientes_{clinicId}_{DateTime.Now:yyyyMMdd}.xlsx";
                    break;
            }

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = $"Error generando reporte de saldos: {ex.Message}" });
        }
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private Guid? GetCurrentUserClinicId()
    {
        var claim = User.FindFirst("ClinicId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public record RegisterPaymentRequestDto(
    Guid ClinicId,
    Guid PatientId,
    Guid? AppointmentId,
    decimal Amount,
    PaymentMethod Method,
    string Concept,
    string? Notes,
    DateTime? PaidAt
);

public record UpdateTotalDueDto(decimal TotalDue);
