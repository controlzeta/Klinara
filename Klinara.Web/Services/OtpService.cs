using System;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace Klinara.Web.Services;

public interface IOtpService
{
    Task<string> GenerateOtpAsync(ApplicationUser user, string purpose);
    Task<bool> ValidateOtpAsync(ApplicationUser user, string code, string purpose);
    Task InvalidateOtpAsync(ApplicationUser user, string purpose);
    Task<bool> IsOtpLockedOutAsync(ApplicationUser user);
    Task RecordFailedAttemptAsync(ApplicationUser user);
    Task ResetFailedAttemptsAsync(ApplicationUser user);
}

public class OtpService : IOtpService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    // Configuración de strike blocking
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);

    public OtpService(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    public async Task<string> GenerateOtpAsync(ApplicationUser user, string purpose)
    {
        var token = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, purpose);
        return token[..6]; // Tomar primeros 6 dígitos para código OTP
    }

    public async Task<bool> ValidateOtpAsync(ApplicationUser user, string code, string purpose)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return false;

        // Verificar lockout por intentos fallidos
        if (await IsOtpLockedOutAsync(user))
            return false;

        var isValid = await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, purpose, code);

        if (isValid)
        {
            // Éxito: resetear intentos fallidos
            await ResetFailedAttemptsAsync(user);
        }
        else
        {
            // Fallo: incrementar contador
            await RecordFailedAttemptAsync(user);
        }

        return isValid;
    }

    public async Task InvalidateOtpAsync(ApplicationUser user, string purpose)
    {
        await _userManager.UpdateSecurityStampAsync(user);
    }

    public async Task<bool> IsOtpLockedOutAsync(ApplicationUser user)
    {
        if (user.OtpLockoutEnd.HasValue && user.OtpLockoutEnd.Value > DateTime.UtcNow)
            return true;
        return false;
    }

    public async Task RecordFailedAttemptAsync(ApplicationUser user)
    {
        var now = DateTime.UtcNow;
        
        // Si el último intento fue hace más de la ventana, resetear contador
        if (user.OtpLastFailedAt.HasValue && (now - user.OtpLastFailedAt.Value) > AttemptWindow)
        {
            user.OtpFailedAttempts = 0;
        }

        user.OtpFailedAttempts++;
        user.OtpLastFailedAt = now;

        // Aplicar lockout si alcanza el máximo
        if (user.OtpFailedAttempts >= MaxFailedAttempts)
        {
            user.OtpLockoutEnd = now.Add(LockoutDuration);
        }

        await _userManager.UpdateAsync(user);
    }

    public async Task ResetFailedAttemptsAsync(ApplicationUser user)
    {
        user.OtpFailedAttempts = 0;
        user.OtpLastFailedAt = null;
        user.OtpLockoutEnd = null;
        await _userManager.UpdateAsync(user);
    }
}

// Propósitos OTP estándar
public static class OtpPurposes
{
    public const string Login = "LOGIN";
    public const string EmailVerification = "EMAIL_VERIFICATION";
    public const string PasswordReset = "PASSWORD_RESET";
    public const string PatientPortalAccess = "PATIENT_PORTAL";

    // Propósitos con alcance por clínica (para aislamiento multi-tenant)
    public static string EmailVerificationForClinic(Guid clinicId) => $"{EmailVerification}:{clinicId}";
    public static string PatientPortalAccessForClinic(Guid clinicId) => $"{PatientPortalAccess}:{clinicId}";
}