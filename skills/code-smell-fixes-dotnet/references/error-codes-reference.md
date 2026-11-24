# Error Codes Reference for Klinara

## Centralized Error Message Constants

Created to replace magic strings inline in service methods. All error messages used in `PatientPortalService.cs`, `AppointmentService.cs`, and other service classes should reference this file.

### Error Codes Catalog

| Code Constant | Message | Usage Location |
|--------------|---------|----------------|
| `ErrorCodes.ClinicNotFound` | "Clínica no encontrada" | `PatientPortalService.SendVerificationEmailAsync`, `ValidateOtpAndCreatePortalTokenAsync` |
| `ErrorCodes.UserNotFound` | "Usuario no encontrado" | `PatientPortalService.ValidateOtpAndCreatePortalTokenAsync` |
| `ErrorCodes.PatientNotFound` | "Paciente no encontrado" | `PatientPortalService.BookAppointmentAsync`, `CancelAppointmentAsync` |
| `ErrorCodes.PatientBlocked` | "El paciente está bloqueado para reservas por impuntualidades repetidas" | `PatientPortalService` (multiple locations) |
| `ErrorCodes.PortalAccessTokenInvalid` | "Token de acceso inválido, expirado o ya usado" | `PatientPortalService.ValidateAndConsumeTokenAsync` |
| `ErrorCodes.DoctorNotFoundInClinic` | "Doctor no encontrado en esta clínica" | `PatientPortalService.BookAppointmentAsync` |
| `ErrorCodes.OtpSendError` | "Error interno al enviar el código de verificación" | `PatientPortalService.SendVerificationEmailAsync` |
| `ErrorCodes.OtpInvalid` | "Código de verificación inválido o expirado" | `PatientPortalService.ValidateOtpAndCreatePortalTokenAsync` |
| `ErrorCodes.ConfirmationTokenInvalid` | "Token de confirmación inválido, expirado o ya usado" | `PatientPortalService.ConfirmAppointmentAsync` |
| `ErrorCodes.AppointmentNotFound` | "Cita no encontrada" | `PatientPortalService.ConfirmAppointmentAsync`, `CancelAppointmentAsync` |
| `ErrorCodes.AppointmentNotScheduled` | "La cita no está en estado programado" | `PatientPortalService.ConfirmAppointmentAsync` |
| `ErrorCodes.NoScheduledAppointments` | "No hay citas programadas para cancelar" | `PatientPortalService.CancelAppointmentAsync` |

## Usage Pattern

```csharp
// Before (magic string - anti-pattern):
return new PortalVerificationResult(false, "Clínica no encontrada", email);

// After (centralized - recommended):
return new PortalVerificationResult(false, ErrorCodes.ClinicNotFound, email);
```

## Namespace
`Klinara.Web.Services` — import this namespace in any file that uses `ErrorCodes.Xxx`.

## Related
- `Klinara.Web/Services/ErrorCodes.cs` — The source file with the const definitions
- `Klinara.Web/Services/PatientPortalService.cs` — Updated to use these codes (19 magic strings replaced)
- `Klinara.Web/Services/IPatientPortalService.cs` — Interface already declares return types that carry error strings
- `Klinara.Web/Services/ErrorCodes.cs` — New file created in current session