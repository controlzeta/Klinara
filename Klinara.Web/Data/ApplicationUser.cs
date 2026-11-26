using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Klinara.Web.Data;

public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Nombre completo del usuario (para doctores, empleados, admins)
    /// </summary>
    [MaxLength(200)]
    public string? FullName { get; set; }

    /// <summary>
    /// Teléfono del usuario
    /// </summary>
    [MaxLength(50)]
    public string? Phone { get; set; }

    /// <summary>
    /// Indica si el usuario está activo
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Fecha de creación del usuario
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha de última actualización
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Clínica a la que pertenece el usuario. Null para Administrador de Plataforma.
    /// </summary>
    public Guid? ClinicId { get; set; }

    [ForeignKey(nameof(ClinicId))]
    public virtual Clinic? Clinic { get; set; }

    /// <summary>
    /// Intentos fallidos de OTP consecutivos (para lockout temporal)
    /// </summary>
    public int OtpFailedAttempts { get; set; } = 0;

    /// <summary>
    /// Timestamp del último intento fallido de OTP
    /// </summary>
    public DateTime? OtpLastFailedAt { get; set; }

    /// <summary>
    /// Bloqueo temporal por OTP (expiración)
    /// </summary>
    public DateTime? OtpLockoutEnd { get; set; }

    /// <summary>
    /// Indica si el usuario debe cambiar contraseña en próximo login
    /// </summary>
    public bool MustChangePassword { get; set; } = false;

    /// <summary>
    /// Último login exitoso (para auditoría)
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}

