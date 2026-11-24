using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Klinara.Web.Data;

namespace Klinara.Web.Services;

public interface IPatientPortalService
{
    Task<PortalVerificationResult> SendVerificationEmailAsync(string email, string clinicSubdomain);
    Task<PortalTokenResult> ValidateOtpAndCreatePortalTokenAsync(string email, string code, string clinicSubdomain);
    Task<PortalBookingResult> BookAppointmentAsync(string portalToken, BookAppointmentDto dto);
    Task<PortalConfirmationResult> ConfirmAppointmentAsync(string confirmationToken);
    Task<PortalAppointmentView?> GetAppointmentViewAsync(string portalToken);
    Task<PortalCancellationResult> CancelAppointmentAsync(string portalToken, string reason);
    Task<List<DoctorAvailabilityDto>> GetAvailableDoctorsAsync(string clinicSubdomain);
    Task<List<TimeSlot>> GetAvailableSlotsAsync(string clinicSubdomain, Guid doctorId, DateTime startDate, DateTime endDate);
}

public record PortalVerificationResult(
    bool Success,
    string? Error = null,
    string? Email = null
);

public record PortalTokenResult(
    bool Success,
    string? PortalToken = null,
    DateTime? ExpiresAt = null,
    string? Error = null,
    Guid? PatientId = null
);

public record BookAppointmentDto(
    Guid DoctorId,
    DateTime StartAt,
    DateTime EndAt,
    AppointmentType Type,
    string? Notes
);

public record PortalBookingResult(
    bool Success,
    Guid? AppointmentId = null,
    string? ConfirmationToken = null,
    DateTime? ConfirmationExpiresAt = null,
    string? Error = null
);

public record PortalConfirmationResult(
    bool Success,
    Guid? AppointmentId = null,
    string? Error = null
);

public record PortalAppointmentView(
    Guid AppointmentId,
    Guid ClinicId,
    string ClinicName,
    string DoctorName,
    string PatientName,
    DateTime StartAt,
    DateTime EndAt,
    AppointmentType Type,
    AppointmentStatus Status,
    string? Notes,
    string? CancellationReason
);

public record PortalCancellationResult(
    bool Success,
    bool IsLateCancellation = false,
    int LateCancellationCount = 0,
    bool IsBlocked = false,
    string? Error = null
);

public record DoctorAvailabilityDto(
    Guid DoctorId,
    string DoctorName,
    string? Specialty,
    List<DayScheduleDto> WeeklySchedule
);

public record DayScheduleDto(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeOnly? LunchStart,
    TimeOnly? LunchEnd,
    int SlotDurationMinutes
);