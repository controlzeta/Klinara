using System.Text;

namespace Klinara.Web.Services;

/// <summary>
/// Plantillas de email con estilos Klinara (color-scheme: light)
/// </summary>
public static class KlinaraEmailTemplates
{
    // Color primario Klinara
    private const string PrimaryColor = "#155EEF";
    private const string PrimaryColorDark = "#0d47c7";
    private const string BackgroundColor = "#f8f9fa";
    private const string CardBackground = "#ffffff";
    private const string TextPrimary = "#212529";
    private const string TextSecondary = "#6c757d";
    private const string BorderColor = "#dee2e6";

    /// <summary>
    /// Wrapper principal para todos los emails Klinara
    /// </summary>
    public static string Wrap(string title, string preheader, string content, string? clinicName = null, string? footerNote = null)
    {
        var clinicHeader = string.IsNullOrEmpty(clinicName) ? "Klinara" : clinicName;
        
        return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <meta name=""color-scheme"" content=""light"">
    <meta name=""supported-color-schemes"" content=""light"">
    <title>{title}</title>
    <!--[if mso]>
    <noscript>
        <xml>
            <o:OfficeDocumentSettings>
                <o:PixelsPerInch>96</o:PixelsPerInch>
            </o:OfficeDocumentSettings>
        </xml>
    </noscript>
    <![endif]-->
</head>
<body style=""margin: 0; padding: 0; background-color: {BackgroundColor}; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; color: {TextPrimary}; line-height: 1.6;"">
    <div style=""display: none; max-height: 0; overflow: hidden;"">{preheader}</div>
    
    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background-color: {BackgroundColor}; padding: 40px 20px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""100%"" max-width=""600px"" cellspacing=""0"" cellpadding=""0"" style=""background-color: {CardBackground}; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.08); overflow: hidden;"">
                    
                    <!-- Header -->
                    <tr>
                        <td style=""background-color: {PrimaryColor}; padding: 32px 24px; text-align: center;"">
                            <div style=""width: 56px; height: 56px; background-color: rgba(255,255,255,0.2); border-radius: 12px; margin: 0 auto 16px; display: flex; align-items: center; justify-content: center;"">
                                <svg width=""28"" height=""28"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"" style=""display: block;"">
                                    <path d=""M10 3h4v7h7v4h-7v7h-4v-7H3v-4h7z"" fill=""white""/>
                                </svg>
                            </div>
                            <h1 style=""margin: 0; color: white; font-size: 24px; font-weight: 700; letter-spacing: -0.5px;"">Klinara</h1>
                            <p style=""margin: 8px 0 0; color: rgba(255,255,255,0.9); font-size: 14px;"">Gestión clínica inteligente</p>
                        </td>
                    </tr>
                    
                    <!-- Clinic badge (optional) -->
                    {(string.IsNullOrEmpty(clinicName) ? "" : $@"
                    <tr>
                        <td style=""padding: 16px 24px 0;"">
                            <div style=""display: inline-block; background-color: {PrimaryColor}15; color: {PrimaryColor}; padding: 6px 12px; border-radius: 20px; font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;"">
                                {clinicName}
                            </div>
                        </td>
                    </tr>
                    ")}
                    
                    <!-- Content -->
                    <tr>
                        <td style=""padding: 32px 24px;"">
                            {content}
                        </td>
                    </tr>
                    
                    <!-- Divider -->
                    <tr>
                        <td style=""padding: 0 24px 24px;"">
                            <hr style=""border: none; border-top: 1px solid {BorderColor}; margin: 0;"">
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style=""padding: 0 24px 32px; text-align: center;"">
                            <p style=""margin: 0 0 12px; color: {TextSecondary}; font-size: 13px;"">
                                Este correo fue enviado por <strong>Klinara</strong> — Plataforma de gestión para consultorios.
                            </p>
                            <p style=""margin: 0 0 16px; color: {TextSecondary}; font-size: 12px;"">
                                Si no solicitaste esta acción, puedes ignorar este correo de forma segura.
                            </p>
                            {(string.IsNullOrEmpty(footerNote) ? "" : $@"
                            <p style=""margin: 16px 0 0; padding-top: 16px; border-top: 1px solid {BorderColor}; color: {TextSecondary}; font-size: 12px;"">
                                {footerNote}
                            </p>
                            ")}
                            <p style=""margin: 16px 0 0; color: {TextSecondary}; font-size: 11px;"">
                                © {DateTime.UtcNow.Year} Klinara. Todos los derechos reservados.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    /// <summary>
    /// Botón principal estilo Klinara
    /// </summary>
    public static string PrimaryButton(string text, string url, string? width = null)
    {
        var widthStyle = string.IsNullOrEmpty(width) ? "" : $"width: {width};";
        return $@"
            <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" style=""margin: 24px auto;"">
                <tr>
                    <td style=""border-radius: 8px; background-color: {PrimaryColor}; {widthStyle}"" align=""center"">
                        <a href=""{url}"" style=""display: inline-block; padding: 14px 32px; color: white; text-decoration: none; font-weight: 600; font-size: 16px; border-radius: 8px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;"">{text}</a>
                    </td>
                </tr>
            </table>";
    }

    /// <summary>
    /// Código OTP grande y legible
    /// </summary>
    public static string OtpCode(string code)
    {
        return $@"
            <div style=""text-align: center; margin: 32px 0;"">
                <div style=""display: inline-block; background-color: {PrimaryColor}10; border: 2px dashed {PrimaryColor}; border-radius: 12px; padding: 24px 40px;"">
                    <span style=""font-family: 'SF Mono', 'Fira Code', 'Consolas', monospace; font-size: 36px; font-weight: 700; letter-spacing: 8px; color: {PrimaryColor}; text-transform: uppercase;"">{code}</span>
                </div>
                <p style=""margin: 16px 0 0; color: {TextSecondary}; font-size: 14px;"">Este código expira en 1 hora</p>
            </div>";
    }

    /// <summary>
    /// Fila de información (label + value)
    /// </summary>
    public static string InfoRow(string label, string value, string? valueColor = null)
    {
        return $@"
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin: 12px 0;"">
                <tr>
                    <td style=""padding: 12px 16px; background-color: {BackgroundColor}; border-radius: 8px; border-left: 4px solid {PrimaryColor};"">
                        <p style=""margin: 0; color: {TextSecondary}; font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;"">{label}</p>
                        <p style=""margin: 4px 0 0; color: {valueColor ?? TextPrimary}; font-size: 16px; font-weight: 500;"">{value}</p>
                    </td>
                </tr>
            </table>";
    }

    /// <summary>
    /// Plantilla: Confirmación de cuenta
    /// </summary>
    public static string AccountConfirmation(string userName, string confirmationLink, string? clinicName = null)
    {
        var content = new StringBuilder();
        content.AppendLine("<h2 style=\"margin: 0 0 16px; font-size: 22px; font-weight: 700; color: \" + TextPrimary + \";\">Bienvenido a Klinara</h2>");
        content.AppendLine("<p style=\"margin: 0 0 24px; color: \" + TextSecondary + \"; font-size: 16px;\">Hola <strong>" + userName + "</strong>,</p>");
        content.AppendLine("<p style=\"margin: 0 0 24px; color: \" + TextPrimary + \"; font-size: 16px;\">Gracias por registrarte. Por favor confirma tu cuenta haciendo clic en el botón:</p>");
        content.Append(PrimaryButton("Confirmar mi cuenta", confirmationLink));
        content.AppendLine("<p style=\"margin: 24px 0 0; color: \" + TextSecondary + \"; font-size: 14px;\">Este enlace expira en 24 horas.</p>");
        content.AppendLine("<p style=\"margin: 16px 0 0; color: \" + TextSecondary + \"; font-size: 14px;\">Si no creaste esta cuenta, puedes ignorar este correo de forma segura.</p>");

        return Wrap("Confirma tu cuenta en Klinara", "Confirma tu cuenta en Klinara para acceder a tu consultorio.", content.ToString(), clinicName);
    }

    /// <summary>
    /// Plantilla: Restablecimiento de contraseña (enlace)
    /// </summary>
    public static string PasswordResetLink(string userName, string resetLink, string? clinicName = null)
    {
        var content = new StringBuilder();
        content.AppendLine("<h2 style=\"margin: 0 0 16px; font-size: 22px; font-weight: 700; color: \" + TextPrimary + \";\">Restablece tu contraseña</h2>");
        content.AppendLine("<p style=\"margin: 0 0 24px; color: \" + TextSecondary + \"; font-size: 16px;\">Hola <strong>" + userName + "</strong>,</p>");
        content.AppendLine("<p style=\"margin: 0 0 24px; color: \" + TextPrimary + \"; font-size: 16px;\">Recibimos una solicitud para restablecer tu contraseña. Haz clic en el botón para crear una nueva:</p>");
        content.Append(PrimaryButton("Restablecer contraseña", resetLink));
        content.AppendLine("<p style=\"margin: 24px 0 0; color: \" + TextSecondary + \"; font-size: 14px;\">Este enlace expira en 1 hora.</p>");
        content.AppendLine("<p style=\"margin: 16px 0 0; color: \" + TextSecondary + \"; font-size: 14px;\">Si no solicitaste este cambio, puedes ignorar este correo de forma segura.</p>");

        return Wrap("Restablece tu contraseña en Klinara", "Restablece tu contraseña en Klinara.", content.ToString(), clinicName);
    }

    /// <summary>
    /// Plantilla: Código OTP de 6 dígitos (verificación / restablecimiento)
    /// </summary>
    public static string OtpCodeEmail(string userName, string code, string purpose, string? clinicName = null)
    {
        var title = purpose == "EmailVerification" ? "Tu código de verificación" : "Tu código de restablecimiento";
        var preheader = purpose == "EmailVerification" ? "Tu código de verificación de 6 dígitos para acceder a Klinara." : "Tu código de 6 dígitos para restablecer la contraseña.";

        var content = new StringBuilder();
        content.AppendLine("<h2 style=\"margin: 0 0 16px; font-size: 22px; font-weight: 700; color: \" + TextPrimary + \";\">" + title + "</h2>");
        content.AppendLine("<p style=\"margin: 0 0 24px; color: \" + TextSecondary + \"; font-size: 16px;\">Hola <strong>" + userName + "</strong>,</p>");
        content.AppendLine("<p style=\"margin: 0 0 24px; color: \" + TextPrimary + \"; font-size: 16px;\">Tu código de verificación de 6 dígitos es:</p>");
        content.Append(OtpCode(code));
        content.AppendLine("<p style=\"margin: 16px 0 0; color: \" + TextSecondary + \"; font-size: 14px;\">Si no solicitaste este código, ignora este correo.</p>");

        return Wrap(title + " - Klinara", preheader, content.ToString(), clinicName);
    }

    /// <summary>
    /// Plantilla: Confirmación de cita (portal paciente)
    /// </summary>
    public static string AppointmentConfirmation(string patientName, string doctorName, DateTime startAt, TimeSpan duration, string appointmentType, string? notes, string confirmUrl, string clinicName, string? clinicSubdomain = null)
    {
        var formattedDate = startAt.ToString("dddd, dd 'de' MMMM 'de' yyyy 'a las' HH:mm", new System.Globalization.CultureInfo("es-ES"));
        var durationMinutes = (int)duration.TotalMinutes;

        var content = new StringBuilder();
        content.AppendLine("<h2 style=\"margin: 0 0 16px; font-size: 22px; font-weight: 700; color: \" + TextPrimary + \";\">Confirmación de cita</h2>");
        content.AppendLine("<p style=\"margin: 0 0 24px; color: \" + TextSecondary + \"; font-size: 16px;\">Hola <strong>" + patientName + "</strong>,</p>");
        content.AppendLine("<p style=\"margin: 0 0 24px; color: \" + TextPrimary + \"; font-size: 16px;\">Has reservado una cita con <strong>" + doctorName + "</strong>.</p>");
        
        content.Append(InfoRow("Fecha y hora", formattedDate + " (zona horaria de la clínica)"));
        content.Append(InfoRow("Duración", durationMinutes + " minutos"));
        content.Append(InfoRow("Tipo de cita", appointmentType));
        
        if (!string.IsNullOrWhiteSpace(notes))
        {
            content.Append(InfoRow("Notas", notes));
        }

        content.AppendLine("<p style=\"margin: 24px 0 8px; color: \" + TextPrimary + \"; font-size: 16px;\">Por favor confirma tu cita:</p>");
        content.Append(PrimaryButton("Confirmar mi cita", confirmUrl));
        
        content.AppendLine("<p style=\"margin: 24px 0 0; color: \" + TextSecondary + \"; font-size: 14px;\">Este enlace expira en 24 horas.</p>");
        content.AppendLine("<p style=\"margin: 16px 0 0; color: \" + TextSecondary + \"; font-size: 14px;\">Si no reservaste esta cita, puedes ignorar este correo.</p>");

        var footerNote = "Cita agendada en " + clinicName + (string.IsNullOrEmpty(clinicSubdomain) ? "" : " (" + clinicSubdomain + ")");

        return Wrap("Confirma tu cita - " + clinicName, "Confirma tu cita agendada en " + clinicName + ".", content.ToString(), clinicName, footerNote);
    }
}