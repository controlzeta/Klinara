using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Klinara.Web.Data;

public class ApplicationUser : IdentityUser<Guid>
{
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

