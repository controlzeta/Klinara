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
}

public class OtpService : IOtpService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

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

        return await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, purpose, code);
    }

    public async Task InvalidateOtpAsync(ApplicationUser user, string purpose)
    {
        await _userManager.UpdateSecurityStampAsync(user);
    }
}

// Propósitos OTP estándar
public static class OtpPurposes
{
    public const string Login = "LOGIN";
    public const string EmailVerification = "EMAIL_VERIFICATION";
    public const string PasswordReset = "PASSWORD_RESET";
    public const string PatientPortalAccess = "PATIENT_PORTAL";
}