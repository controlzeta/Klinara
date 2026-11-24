using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Klinara.Web.Services;

public class LoggingEmailSender : IEmailSender<ApplicationUser>
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        // NO loguear el link completo (contiene token)
        _logger.LogInformation("Email de confirmación solicitado para {Email} (UserId: {UserId})", email, user.Id);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        _logger.LogInformation("Email de recuperación de contraseña solicitado para {Email} (UserId: {UserId})", email, user.Id);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        // NO loguear el código OTP
        _logger.LogInformation("Código de recuperación enviado a {Email} (UserId: {UserId})", email, user.Id);
        return Task.CompletedTask;
    }
}

// Implementación para OTP de login (usa UserManager tokens)
public class OtpEmailSender
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<OtpEmailSender> _logger;
    private readonly IOtpService _otpService;

    public OtpEmailSender(UserManager<ApplicationUser> userManager, ILogger<OtpEmailSender> logger, IOtpService otpService)
    {
        _userManager = userManager;
        _logger = logger;
        _otpService = otpService;
    }

    public async Task<string> SendLoginOtpAsync(ApplicationUser user)
    {
        var otp = await _otpService.GenerateOtpAsync(user, OtpPurposes.Login);
        
        // En producción: enviar email real con el OTP
        // await _emailService.SendAsync(user.Email!, "Tu código de acceso Klinara", $"Tu código es: {otp}. Expira en 1 hora.");
        
        // LOG SEGURO: Solo metadata, NUNCA el OTP
        _logger.LogInformation("OTP de login generado para {Email} (UserId: {UserId}, Purpose: {Purpose})", 
            user.Email, user.Id, OtpPurposes.Login);
        
        return otp; // Solo para testing/dev; en prod no retornar
    }

    public async Task<bool> ValidateLoginOtpAsync(ApplicationUser user, string code)
    {
        var isValid = await _otpService.ValidateOtpAsync(user, code, OtpPurposes.Login);
        
        if (isValid)
        {
            await _otpService.InvalidateOtpAsync(user, OtpPurposes.Login);
            _logger.LogInformation("OTP de login validado correctamente para {Email} (UserId: {UserId})", user.Email, user.Id);
        }
        else
        {
            _logger.LogWarning("OTP de login INVÁLIDO para {Email} (UserId: {UserId})", user.Email, user.Id);
        }
        
        return isValid;
    }
}