using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Klinara.Web.Data;

public enum AppointmentStatus
{
    Scheduled = 1,
    Confirmed = 2,
    CancelledByPatient = 3,
    CancelledByStaff = 4,
    NoShow = 5,
    Completed = 6,
    Rescheduled = 7
}

public enum AppointmentType
{
    FirstVisit = 1,
    FollowUp = 2,
    Procedure = 3,
    Telemedicine = 4,
    Emergency = 5
}

public class Appointment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ClinicId { get; set; }

    [ForeignKey(nameof(ClinicId))]
    public virtual Clinic Clinic { get; set; } = null!;

    [Required]
    public Guid DoctorId { get; set; }

    [ForeignKey(nameof(DoctorId))]
    public virtual ApplicationUser Doctor { get; set; } = null!;

    [Required]
    public Guid PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public virtual Patient Patient { get; set; } = null!;

    [Required]
    public DateTime StartAt { get; set; }

    [Required]
    public DateTime EndAt { get; set; }

    [Required]
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    public AppointmentType Type { get; set; } = AppointmentType.FollowUp;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(200)]
    public string? CancellationReason { get; set; }

    public Guid? CancelledByUserId { get; set; }

    [ForeignKey(nameof(CancelledByUserId))]
    public virtual ApplicationUser? CancelledByUser { get; set; }

    public DateTime? CancelledAt { get; set; }

    public bool IsLateCancellation { get; set; } = false;

    public bool IsNoShow { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public virtual ApplicationUser CreatedByUser { get; set; } = null!;

    // Navigation
    public virtual ICollection<AppointmentHistory> History { get; set; } = new List<AppointmentHistory>();
}

public class AppointmentHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid AppointmentId { get; set; }

    [ForeignKey(nameof(AppointmentId))]
    public virtual Appointment Appointment { get; set; } = null!;

    [Required]
    public DateTime At { get; set; } = DateTime.UtcNow;

    [Required]
    public AppointmentStatus FromStatus { get; set; }

    [Required]
    public AppointmentStatus ToStatus { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    public Guid? ChangedByUserId { get; set; }

    [ForeignKey(nameof(ChangedByUserId))]
    public virtual ApplicationUser? ChangedByUser { get; set; }
}

public class DoctorSchedule
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ClinicId { get; set; }

    [ForeignKey(nameof(ClinicId))]
    public virtual Clinic Clinic { get; set; } = null!;

    [Required]
    public Guid DoctorId { get; set; }

    [ForeignKey(nameof(DoctorId))]
    public virtual ApplicationUser Doctor { get; set; } = null!;

    [Required]
    public DayOfWeek DayOfWeek { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    public TimeOnly? LunchStart { get; set; }

    public TimeOnly? LunchEnd { get; set; }

    [Range(5, 120)]
    public int SlotDurationMinutes { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;

    public DateTime? EffectiveTo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class TimeSlot
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ClinicId { get; set; }

    [ForeignKey(nameof(ClinicId))]
    public virtual Clinic Clinic { get; set; } = null!;

    [Required]
    public Guid DoctorId { get; set; }

    [ForeignKey(nameof(DoctorId))]
    public virtual ApplicationUser Doctor { get; set; } = null!;

    [Required]
    public DateTime StartAt { get; set; }

    [Required]
    public DateTime EndAt { get; set; }

    public bool IsBooked { get; set; } = false;

    public Guid? AppointmentId { get; set; }

    [ForeignKey(nameof(AppointmentId))]
    public virtual Appointment? Appointment { get; set; }

    public bool IsBlocked { get; set; } = false;

    [MaxLength(200)]
    public string? BlockReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ClinicHoliday
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ClinicId { get; set; }

    [ForeignKey(nameof(ClinicId))]
    public virtual Clinic Clinic { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime Date { get; set; }

    public bool IsRecurring { get; set; } = false;

    public bool IsFullDay { get; set; } = true;

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DoctorVacation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ClinicId { get; set; }

    [ForeignKey(nameof(ClinicId))]
    public virtual Clinic Clinic { get; set; } = null!;

    [Required]
    public Guid DoctorId { get; set; }

    [ForeignKey(nameof(DoctorId))]
    public virtual ApplicationUser Doctor { get; set; } = null!;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Patient
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ClinicId { get; set; }

    [ForeignKey(nameof(ClinicId))]
    public virtual Clinic Clinic { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(10)]
    public string? Gender { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int LateCancellationCount { get; set; } = 0;

    public int NoShowCount { get; set; } = 0;

    public bool IsBlocked { get; set; } = false;

    public DateTime? BlockedUntil { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDue { get; set; } = 0m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public virtual ApplicationUser CreatedByUser { get; set; } = null!;

    // Navigation
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public virtual ICollection<PatientToken> Tokens { get; set; } = new List<PatientToken>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public enum PatientTokenType
{
    PortalAccess = 1,
    AppointmentConfirmation = 2,
    AppointmentReminder = 3,
    EmailVerification = 4,
    AppointmentManagement = 5
}

public class PatientToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public virtual Patient Patient { get; set; } = null!;

    [Required]
    public Guid ClinicId { get; set; }

    [ForeignKey(nameof(ClinicId))]
    public virtual Clinic Clinic { get; set; } = null!;

    [Required]
    public PatientTokenType Type { get; set; }

    [Required]
    [MaxLength(100)]
    public string Token { get; set; } = string.Empty;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public Guid? AppointmentId { get; set; }

    [ForeignKey(nameof(AppointmentId))]
    public virtual Appointment? Appointment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OtpResendLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public Guid ClinicId { get; set; }

    [ForeignKey(nameof(ClinicId))]
    public virtual Clinic Clinic { get; set; } = null!;

    [Required]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}