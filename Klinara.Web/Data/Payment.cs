using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Klinara.Web.Data;

public enum PaymentMethod
{
    Cash = 1,          // Efectivo
    CreditCard = 2,    // Tarjeta de crédito
    DebitCard = 3,     // Tarjeta de débito
    SPEI = 4,          // Transferencia SPEI
    Other = 5          // Otro
}

public class Payment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ClinicId { get; set; }

    [ForeignKey(nameof(ClinicId))]
    public virtual Clinic Clinic { get; set; } = null!;

    [Required]
    public Guid PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public virtual Patient Patient { get; set; } = null!;

    public Guid? AppointmentId { get; set; }

    [ForeignKey(nameof(AppointmentId))]
    public virtual Appointment? Appointment { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [Required]
    [MaxLength(200)]
    public string Concept { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public Guid CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public virtual ApplicationUser CreatedByUser { get; set; } = null!;
}
