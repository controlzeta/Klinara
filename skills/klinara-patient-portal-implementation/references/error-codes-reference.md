# Error Codes Reference

## ErrorCodes Constants Catalog

| Constant | Meaning | Location |
|----------|---------|----------|
| `ErrorCodes.ClinicNotFound` | Clínica no encontrada para el subdominio especificado | `Klinara.Web/Services/ErrorCodes.cs` |
| `ErrorCodes.UserNotFound` | Usuario no encontrado en la clínica especificada | `Klinara.Web/Services/ErrorCodes.cs` |
| `ErrorCodes.PatientNotFound` | Paciente no encontrado en la clínica especificada | `Klinara.Web/Services/ErrorCodes.cs` |
| `ErrorCodes.PatientBlocked` | El paciente está bloqueado para reservas por impuntualidades repetidas | `Klinara.Web/Services/ErrorCodes.cs` |
| `ErrorCodes.PortalAccessTokenInvalid` | Token de acceso inválido, expirado o ya usado | `Klinara.Web/Services/ErrorCodes.cs` |
| `ErrorCodes.DoctorNotFoundInClinic` | Doctor no encontrado en esta clínica | `Klinara.Web/Services/ErrorCodes.cs` |
| `ErrorCodes.OtpSendError` | Error interno al enviar el código de verificación | `Klinara.Web/Services/ErrorCodes.cs` |
| `ErrorCodes.OtpInvalid` | Código de verificación inválido o expirado | `Klinara.Web/Services/ErrorCodes.cs` |
| `ErrorCodes.ConfirmationTokenInvalid` | Token de confirmación inválido, expirado o ya usado | `Klinara.Web/Services/ErrorCodes.cs` |
| `ErrorCodes.AppointmentNotFound` | Cita no encontrada | `Klinara.Web/Services/ErrorCodes.cs` |
| `ErrorCodes.AppointmentNotScheduled` | La cita no está en estado programado | `Klinara.Web/Services/ErrorCodes.cs` |
| `ErrorCodes.NoScheduledAppointments` | No hay citas programadas para cancelar | `Klinara.Web/Services/ErrorCodes.cs` |

## Usage Pattern

```csharp
// Before (magic string):
return new PortalVerificationResult(false, "Clínica no encontrada");

// After (ErrorCodes):
return new PortalVerificationResult(false, ErrorCodes.ClinicNotFound);
```

## Migration

1. Create `Klinara.Web/Services/ErrorCodes.cs` with all constants
2. Run: `sed -i 's/"Clínica no encontrada"/ErrorCodes.ClinicNotFound/g' Klinara.Web/Services/*.cs`
3. Replace remaining magic strings manually, verifying each replacement
4. Build verify: `dotnet build` must pass with 0 errors