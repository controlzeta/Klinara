/// <summary>
/// Error codes and messages for the patient portal service.
/// Centraliza los mensajes de error para consistencia y mantenimiento.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Clínica no encontrada para el subdominio especificado</summary>
    public const string ClinicNotFound = "Clínica no encontrada";

    /// <summary>Usuario no encontrado en la clínica especificada</summary>
    public const string UserNotFound = "Usuario no encontrado";

    /// <summary>Paciente no encontrado en la clínica especificada</summary>
    public const string PatientNotFound = "Paciente no encontrado";

    /// <summary>El paciente está bloqueado para reservas por impuntualidades repetidas</summary>
    public const string PatientBlocked = "El paciente está bloqueado para reservas por impuntualidades repetidas";

    /// <summary>Token de acceso inválido, expirado o ya usado</summary>
    public const string PortalAccessTokenInvalid = "Token de acceso inválido, expirado o ya usado";

    /// <summary>Doctor no encontrado en esta clínica</summary>
    public const string DoctorNotFoundInClinic = "Doctor no encontrado en esta clínica";

    /// <summary>Error interno al enviar el código de verificación</summary>
    public const string OtpSendError = "Error interno al enviar el código de verificación";

    /// <summary>Código de verificación inválido o expirado</summary>
    public const string OtpInvalid = "Código de verificación inválido o expirado";

    /// <summary>Token de confirmación inválido, expirado o ya usado</summary>
    public const string ConfirmationTokenInvalid = "Token de confirmación inválido, expirado o ya usado";

    /// <summary>Cita no encontrada</summary>
    public const string AppointmentNotFound = "Cita no encontrada";

    /// <summary>La cita no está en estado programado</summary>
    public const string AppointmentNotScheduled = "La cita no está en estado programado";

    /// <summary>No hay citas programadas para cancelar</summary>
    public const string NoScheduledAppointments = "No hay citas programadas para cancelar";
}