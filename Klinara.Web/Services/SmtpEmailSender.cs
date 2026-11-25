using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Klinara.Web.Services;

public class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var subject = "Confirma tu cuenta en Klinara";
        var body = KlinaraEmailTemplates.AccountConfirmation(user.UserName ?? email, confirmationLink);

        await SendEmailAsync(email, subject, body);
        _logger.LogInformation("Email de confirmación enviado a {Email} (UserId: {UserId})", email, user.Id);
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var subject = "Restablece tu contraseña en Klinara";
        var body = KlinaraEmailTemplates.PasswordResetLink(user.UserName ?? email, resetLink);

        await SendEmailAsync(email, subject, body);
        _logger.LogInformation("Email de recuperación de contraseña enviado a {Email} (UserId: {UserId})", email, user.Id);
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var subject = "Tu código de restablecimiento - Klinara";
        var body = KlinaraEmailTemplates.OtpCodeEmail(user.UserName ?? email, resetCode, "PasswordReset");

        await SendEmailAsync(email, subject, body);
        _logger.LogInformation("Código de recuperación enviado a {Email} (UserId: {UserId})", email, user.Id);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var smtpHost = _configuration["Email:SmtpHost"] ?? "localhost";
        var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var smtpUser = _configuration["Email:SmtpUser"];
        var smtpPass = _configuration["Email:SmtpPass"];
        var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@klinara.com";
        var fromName = _configuration["Email:FromName"] ?? "Klinara";
        var enableSsl = bool.Parse(_configuration["Email:EnableSsl"] ?? "true");
        // Allow invalid certs (e.g., shared hosting cert mismatch) — configurable for dev
        var allowInvalidCerts = bool.Parse(_configuration["Email:AllowInvalidCertificates"] ?? "false");

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = enableSsl,
            Credentials = string.IsNullOrEmpty(smtpUser) ? null : new NetworkCredential(smtpUser, smtpPass)
        };

        // Configure certificate validation for this send
        var originalCallback = ServicePointManager.ServerCertificateValidationCallback;
        try
        {
            if (allowInvalidCerts)
            {
                ServicePointManager.ServerCertificateValidationCallback = 
                    (sender, certificate, chain, errors) => true;
            }

            await client.SendMailAsync(message);
        }
        finally
        {
            // Restore original callback
            ServicePointManager.ServerCertificateValidationCallback = originalCallback;
        }
    }
}