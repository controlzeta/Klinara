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

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResult>> RegisterPayment([FromBody] RegisterPaymentRequestDto req)
    {
        var userId = GetCurrentUserId();
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
            var balance = await _paymentService.GetPatientBalanceAsync(patientId);
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
        var payments = await _paymentService.GetPaymentsByClinicAsync(clinicId, startDate, endDate);
        return Ok(payments);
    }

    [HttpPut("patients/{patientId:guid}/total-due")]
    public async Task<ActionResult> UpdateTotalDue(Guid patientId, [FromBody] UpdateTotalDueDto req)
    {
        var result = await _paymentService.UpdatePatientTotalDueAsync(patientId, req.TotalDue);
        if (!result.Success)
            return BadRequest(new { Error = result.Error });

        return Ok(new { Success = true });
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
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
