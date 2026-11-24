using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Klinara.Web.Data;

public class Clinic
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public string Subdomain { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    /// <summary>
    /// IANA Time Zone ID (ej. "America/Mexico_City", "Central Standard Time (Mexico)")
    /// Se almacena como string y se convierte a TimeZoneInfo en la capa de aplicación.
    /// </summary>
    [MaxLength(100)]
    public string TimeZoneId { get; set; } = "America/Mexico_City";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

    [NotMapped]
    public TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
}