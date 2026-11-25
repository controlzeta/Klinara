using Klinara.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Klinara.Web.Services;

public interface IPaymentService
{
    Task<PaymentResult> RegisterPaymentAsync(RegisterPaymentDto dto, Guid createdByUserId);
    Task<PatientBalanceDto> GetPatientBalanceAsync(Guid patientId);
    Task<List<PaymentSummaryDto>> GetPaymentsByClinicAsync(Guid clinicId, DateTime? startDate = null, DateTime? endDate = null);
    Task<PaymentResult> UpdatePatientTotalDueAsync(Guid patientId, decimal totalDue);
}

public record RegisterPaymentDto(
    Guid ClinicId,
    Guid PatientId,
    Guid? AppointmentId,
    decimal Amount,
    PaymentMethod Method,
    string Concept,
    string? Notes,
    DateTime? PaidAt
);

public record PaymentResult(
    bool Success,
    Guid? PaymentId = null,
    string? Error = null
);

public record PatientBalanceDto(
    Guid PatientId,
    string PatientName,
    decimal TotalDue,
    decimal TotalPaid,
    decimal Balance,
    List<PaymentItemDto> Payments
);

public record PaymentItemDto(
    Guid Id,
    decimal Amount,
    PaymentMethod Method,
    string Concept,
    DateTime PaidAt,
    string? Notes
);

public record PaymentSummaryDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    decimal Amount,
    PaymentMethod Method,
    string Concept,
    DateTime PaidAt,
    string? AppointmentInfo
);

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(ApplicationDbContext context, ILogger<PaymentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaymentResult> RegisterPaymentAsync(RegisterPaymentDto dto, Guid createdByUserId)
    {
        try
        {
            if (dto.Amount <= 0)
                return new PaymentResult(false, Error: "El monto del pago debe ser mayor a cero.");

            if (string.IsNullOrWhiteSpace(dto.Concept))
                return new PaymentResult(false, Error: "El concepto del pago es obligatorio.");

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == dto.PatientId && p.ClinicId == dto.ClinicId);
            if (patient == null)
                return new PaymentResult(false, Error: "Paciente no encontrado en esta clínica.");

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                ClinicId = dto.ClinicId,
                PatientId = dto.PatientId,
                AppointmentId = dto.AppointmentId,
                Amount = dto.Amount,
                Method = dto.Method,
                Concept = dto.Concept.Trim(),
                Notes = dto.Notes?.Trim(),
                PaidAt = dto.PaidAt ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = createdByUserId
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Pago registrado: {PaymentId} por {Amount} ({Method}) para Paciente {PatientId}",
                payment.Id, payment.Amount, payment.Method, payment.PatientId);

            return new PaymentResult(true, payment.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando pago para paciente {PatientId}", dto.PatientId);
            return new PaymentResult(false, Error: "Error interno al registrar el pago.");
        }
    }

    public async Task<PatientBalanceDto> GetPatientBalanceAsync(Guid patientId)
    {
        var patient = await _context.Patients
            .Include(p => p.Payments)
            .FirstOrDefaultAsync(p => p.Id == patientId);

        if (patient == null)
            throw new KeyNotFoundException("Paciente no encontrado.");

        var totalPaid = patient.Payments.Sum(p => p.Amount);
        var balance = patient.TotalDue - totalPaid;

        var paymentItems = patient.Payments
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new PaymentItemDto(
                p.Id,
                p.Amount,
                p.Method,
                p.Concept,
                p.PaidAt,
                p.Notes
            )).ToList();

        return new PatientBalanceDto(
            patient.Id,
            patient.FullName,
            patient.TotalDue,
            totalPaid,
            balance,
            paymentItems
        );
    }

    public async Task<List<PaymentSummaryDto>> GetPaymentsByClinicAsync(Guid clinicId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Payments
            .Include(p => p.Patient)
            .Include(p => p.Appointment)
            .Where(p => p.ClinicId == clinicId);

        if (startDate.HasValue)
            query = query.Where(p => p.PaidAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(p => p.PaidAt <= endDate.Value);

        return await query
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new PaymentSummaryDto(
                p.Id,
                p.PatientId,
                p.Patient.FullName,
                p.Amount,
                p.Method,
                p.Concept,
                p.PaidAt,
                p.Appointment != null ? p.Appointment.StartAt.ToString("yyyy-MM-dd HH:mm") : null
            ))
            .ToListAsync();
    }

    public async Task<PaymentResult> UpdatePatientTotalDueAsync(Guid patientId, decimal totalDue)
    {
        var patient = await _context.Patients.FindAsync(patientId);
        if (patient == null)
            return new PaymentResult(false, Error: "Paciente no encontrado.");

        patient.TotalDue = Math.Max(0, totalDue);
        patient.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new PaymentResult(true);
    }
}
