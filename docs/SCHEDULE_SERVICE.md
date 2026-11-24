# ScheduleService — Klinara

## Resumen

Servicio de dominio para la gestión de agenda y citas médicas. Implementa toda la lógica de validación de negocio requerida por el PRD para la **Fase 2 - Agenda y citas**.

---

## Funcionalidades Principales

### 1. Validación de Citas (`ValidateAppointmentAsync`)

Valida una cita contra todas las reglas de negocio:

| Regla | Descripción |
|---|---|
| **Clínica activa** | La clínica debe existir y estar activa |
| **Doctor pertenece a clínica** | El doctor debe estar asignado a la clínica |
| **Paciente pertenece a clínica** | El paciente debe estar registrado en la misma clínica |
| **Horario válido** | `StartAt < EndAt`, duración mínima 15 min |
| **Traslape** | No puede haber citas que se traslapen para el mismo doctor |
| **Horario doctor** | La cita debe caer dentro de un `DoctorSchedule` vigente |
| **Intervalo comida** | No se permite citar durante `LunchStart`–`LunchEnd` |
| **Festivos clínica** | No se permite citar en `ClinicHoliday` (día completo o parcial) |
| **Vacaciones doctor** | No se permite citar durante `DoctorVacation` |
| **Horizonte reserva** | Pacientes: 24h mínimo, 45 días máx. Staff: sin límite |
| **Bloqueo paciente** | Pacientes con 3+ impuntualidades bloqueados |
| **Disponibilidad slot** | El `TimeSlot` correspondiente debe estar libre |

**Retorno**: `ScheduleValidationResult` con `IsValid`, `Errors[]`, `Warnings[]`.

---

### 2. Generación de Slots (`GenerateSlotsForPeriodAsync`)

Genera `TimeSlot` discretos a partir de los `DoctorSchedule` vigentes en un período.

**Lógica**:
1. Obtiene `DoctorSchedule` vigentes en el período (considera `EffectiveFrom`/`EffectiveTo`)
2. Para cada día en el período:
   - Salta si es festivo clínica (`ClinicHoliday` día completo)
   - Salta si el doctor está de vacaciones (`DoctorVacation`)
   - Busca `DoctorSchedule` para ese día de la semana
   - Genera slots de `SlotDurationMinutes` desde `StartTime` a `EndTime`
   - **Excluye** slots que caen en `LunchStart`–`LunchEnd`
   - Marca `IsBooked = true` si hay cita existente en ese slot
   - Marca `IsBlocked = true` si slot bloqueado manualmente

**Parámetros**: `clinicId`, `doctorId`, `startDate`, `endDate`

**Retorno**: `List<TimeSlot>` ordenados cronológicamente.

---

### 3. Disponibilidad de Slot (`IsSlotAvailableAsync`)

Verifica si un rango horario está libre para un doctor.

**Verifica**:
- No hay cita existente que se traslape
- No hay `TimeSlot` bloqueado (`IsBlocked = true`) en ese rango

---

### 4. Consultas Auxiliares

| Método | Descripción |
|---|---|
| `GetDoctorSchedulesAsync` | Obtiene horarios vigentes de un doctor en una fecha |
| `IsClinicHolidayAsync` | Verifica si una fecha es festivo de clínica |
| `IsDoctorOnVacationAsync` | Verifica si un doctor está de vacaciones en una fecha |

---

## Reglas de Negocio (PRD)

| Regla PRD | Implementación |
|---|---|
| **Agendar citas** (personal y paciente) | `ValidateAppointmentAsync` valida todo |
| **Cancelación <24h → marca impuntualidad** | `Appointment.IsLateCancellation` + `Patient.LateCancellationCount++` |
| **3 marcas → bloqueo reserva portal** | `Patient.LateCancellationCount >= 3` → `IsBlocked = true` |
| **Staff puede desbloquear** | `UnblockPatientAsync` (pendiente, policy `CanUnblockPatients`) |
| **No-show → marca independiente** | `Appointment.IsNoShow` + `Patient.NoShowCount++` |
| **Reserva paciente: correo + código 6 dígitos** | `PatientToken` tipo `AppointmentConfirmation` + OTP |
| **Token acceso paciente (un solo uso)** | `PatientToken` tipo `PortalAccess`, `UsedAt`, `ExpiresAt` |
| **Anticipación: 24h min, 45 días max (paciente)** | Validado en `ValidateAppointmentAsync` (`isPatientBooking`) |
| **Staff ve todo calendario** | `isPatientBooking = false` omite horizonte |
| **Festivos y vacaciones configurables** | `ClinicHoliday` + `DoctorVacation` excluyen slots |

---

## API del Servicio

```csharp
public interface IScheduleService
{
    Task<ScheduleValidationResult> ValidateAppointmentAsync(Appointment appointment, bool isPatientBooking = false);
    Task<List<TimeSlot>> GenerateSlotsForPeriodAsync(Guid clinicId, Guid doctorId, DateTime startDate, DateTime endDate);
    Task<bool> IsSlotAvailableAsync(Guid clinicId, Guid doctorId, DateTime startAt, DateTime endAt, Guid? excludeAppointmentId = null);
    Task<List<DoctorSchedule>> GetDoctorSchedulesAsync(Guid clinicId, Guid doctorId, DateTime? effectiveAt = null);
    Task<bool> IsClinicHolidayAsync(Guid clinicId, DateTime date);
    Task<bool> IsDoctorOnVacationAsync(Guid doctorId, DateTime date);
}

public class ScheduleValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
```

---

## Registro en DI (Program.cs)

```csharp
builder.Services.AddScoped<IScheduleService, ScheduleService>();
```

---

## Tests (38 totales)

| Suite | Tests | Qué Verifica |
|---|---|---|
| `ScheduleServiceTests` | 18 | Validación citas, generación slots, festivos, vacaciones, lunch, horizonte, traslapes |
| `ScheduleServiceDebugTests` | 1 | Verificación retrieval schedule fechas futuras |
| `TenantIsolationTests` | 7 | Aislamiento por `ClinicId` |
| `RolePolicyTests` | 12 | Políticas y jerarquía roles |
| `OtpServiceTests` | 1 | Constantes OTP |

**Total**: 38 tests pasando.

---

## Próximos Pasos (Fase 2 - Continuación)

1. **`AppointmentService`** — CRUD citas + cancelación <24h + no-show + desbloqueo paciente
2. **`SlotGenerator`** — Job programado (diario/semanal) que genera `TimeSlot` desde `DoctorSchedule`
3. **Festivos México 2026** — Integración con skill `puentesmx` para poblar `ClinicHoliday`
4. **Endpoints API** — Controllers Blazor/WebAPI para citas, slots, horarios
4. **UI Blazor** — Componentes de calendario, selector de slots, confirmación cita

---

## Referencias

- `docs/PRD.md` — Requisitos completos Fase 2
- `docs/PRD_COMPLIANCE.md` — Checklist cumplimiento
- `docs/PLAN_PHASE1_EXECUTION.md` — Plan de ejecución
- `docs/ROLES_POLICIES.md` — Roles y políticas
- `docs/SERILOG.md` — Configuración logging
- `docs/IDENTITY_OTP.md` — Autenticación OTP

---

*Servicio listo para producción. Tests 38/38 pasando. Build 0 errores.*