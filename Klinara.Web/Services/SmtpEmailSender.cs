using System.Net;
using System.Net.Mail;
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
        var body = $@"
            <h2>Bienvenido a Klinara</h2>
            <p>Hola {user.UserName},</p>
            <p>Por favor confirma tu cuenta haciendo clic en el siguiente enlace:</p>
            <p><a href='{confirmationLink}'>Confirmar cuenta</a></p>
            <p>Este enlace expira en 24 horas.</p>
            <p>Si no solicitaste esta cuenta, puedes ignorar este correo.</p>
        ";

        await SendEmailAsync(email, subject, body);
        _logger.LogInformation("Email de confirmación enviado a {Email} (UserId: {UserId})", email, user.Id);
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var subject = "Restablece tu contraseña en Klinara";
        var body = $@"
            <h2>Restablecimiento de contraseña</h2>
            <p>Hola {user.UserName},</p>
            <p>Recibimos una solicitud para restablecer tu contraseña. Haz clic en el siguiente enlace:</p>
            <p><a href='{resetLink}'>Restablecer contraseña</a></p>
            <p>Este enlace expira en 1 hora.</p>
            <p>Si no solicitaste este cambio, puedes ignorar este correo.</p>
        ";

        await SendEmailAsync(email, subject, body);
        _logger.LogInformation("Email de recuperación de contraseña enviado a {Email} (UserId: {UserId})", email, user.Id);
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var subject = "Tu código de restablecimiento - Klinara";
        var body = $@"
            <h2>Código de restablecimiento</h2>
            <p>Hola {user.UserName},</p>
            <p>Tu código de verificación de 6 dígitos es:</p>
            <h1 style='letter-spacing: 4px; font-family: monospace;'>{resetCode}</h1>
            <p>Este código expira en 1 hora.</p>
            <p>Si no solicitaste este código, ignora este correo.</p>
        ";

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

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando email a {Email}: {Message}", toEmail, ex.Message);
            throw;
        }
    }
}