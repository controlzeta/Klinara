// Email templates for Klinara Patient Portal
// These templates are used by the patient portal service to generate email bodies.

public static class EmailTemplates
{
    // OTP verification email template
    public const string OtpVerificationBody = @"
        <h2>Verificación de correo - {clinicName}</h2>
        <p>Tu código de verificación de 6 dígitos es:</p>
        <h1 style='letter-spacing: 4px; font-family: monospace; color: #155EEF;'>{otpCode}</h1>
        <p>Este código expira en 1 hora.</p>
        <p>Si no solicitaste este código, ignora este correo.</p>";

    // Appointment confirmation email template
    public const string AppointmentConfirmationBody = @"
        <h2>Confirmación de cita</h2>
        <p>Hola {patientName},</p>
        <p>Has reservado una cita con <strong>{doctorName}</strong>.</p>
        <p><strong>Fecha y hora:</strong> {dateTime} (zona horaria de la clínica)</p>
        <p><strong>Duración:</strong> {duration} minutos</p>
        <p>Por favor confirma tu cita haciendo clic en el siguiente enlace:</p>
        <p><a href='{confirmUrl}' style='background: #155EEF; color: white; padding: 12px 24px; text-decoration: none; border-radius: 8px; display: inline-block;'>Confirmar cita</a></p>
        <p>Este enlace expira en 24 horas.</p>
        <p>Si no reservaste esta cita, puedes ignorar este correo.</p>";

    // Appointment reminder email template
    public const string AppointmentReminderBody = @"
        <h2>Recordatorio de cita</h2>
        <p>Hola {patientName},</p>
        <p>Te recordamos tu cita con <strong>{doctorName}</strong>.</p>
        <p><strong>Fecha y hora:</strong> {dateTime} (zona horaria de la clínica)</p>
        <p><strong>Duración:</strong> {duration} minutos</p>
        <p>La cita está programada para <strong>{dateLabel}</strong>.</p>
        <p>Por favor confirma o cancela tu cita haciendo clic en los enlaces a continuación.</p>
        <p><a href='{confirmUrl}' style='background: #155EEF; color: white; padding: 12px 24px; text-decoration: none; border-radius: 8px; display: inline-block;'>Confirmar cita</a></p>
        <p><a href='{cancelUrl}' style='background: #f8d7da; color: #721c1c; padding: 12px 24px; text-decoration: none; border-radius: 8px; display: inline-block;'>Cancelar cita</a></p>
        <p>Este enlace expira en 24 horas.</p>";
}