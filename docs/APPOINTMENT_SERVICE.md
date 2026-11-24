# AppointmentService — Klinara

## Resumen

Servicio de dominio para la gestión completa del ciclo de vida de citas médicas. Implementa toda la lógica de negocio para crear, actualizar, cancelar, marcar no-show y desbloquear pacientes, integrándose con `ScheduleService` para validaciones de disponibilidad.

---

## Funcionalidades Principales

### 1. Crear Cita (`CreateAppointmentAsync`)

- Valida la cita usando `ScheduleService.ValidateAppointmentAsync`
- Persiste la cita con estado `Scheduled`
- Genera automáticamente el `TimeSlot` correspondiente
- Registra en log con CorrelationId

### 2. Actualizar Cita (`UpdateAppointmentAsync`)

- Permite reprogramar cita (cambio de horario/doctor)
- Valida nuevos horarios contra `ScheduleService` si hay cambios
- Mantiene historial en `AppointmentHistory` si cambia horario/doctor
- Regenera `TimeSlot` si cambió el horario

### 3. Cancelar Cita (`CancelAppointmentAsync`)

| Escenario | Comportamiento |
|---|---|
| Cancelación por paciente >24h | Estado `CancelledByPatient`, sin penalización |
| Cancelación por paciente <24h | Estado `CancelledByPatient`, `IsLateCancellation=true`, incrementa `Patient.LateCancellationCount` |
| Cancelación por staff <24h | Estado `CancelledByStaff`, `IsLateCancellation=true`, **NO** incrementa contador paciente |
| Cancelación por staff >24h | Estado `CancelledByStaff`, sin penalización |
| 3+ cancelaciones tardías paciente | `Patient.IsBlocked=true`, `BlockedUntil=+3 meses` |

### 4. Marcar No-Show (`MarkNoShowAsync`)

- Solo permitido en citas `Scheduled` o `Confirmed`
- Estado → `NoShow`, `IsNoShow=true`
- Incrementa `Patient.NoShowCount`
- Libera `TimeSlot`
- Registra en `AppointmentHistory`

### 5. Desbloquear Paciente (`UnblockPatientAsync`)

- Requiere permiso `CanUnblockPatients` (AdminClinica, AdminPlataforma)
- Resetea `Patient.IsBlocked=false`, `LateCancellationCount=0`, `BlockedUntil=null`

### 6. Consultas

| Método | Descripción |
|---|---|
| `GetAppointmentAsync` | Cita por ID con includes (Doctor, Patient, CreatedBy, CancelledBy) |
| `GetAppointmentsByClinicAsync` | Citas de clínica con filtro opcional por rango de fechas |
| `GetAppointmentsByDoctorAsync` | Citas de doctor con filtro opcional por rango de fechas |
| `GetAppointmentsByPatientAsync` | Citas de paciente con filtro opcional por rango de fechas |

---

## Reglas de Negocio (PRD)

| Regla PRD | Implementación |
|---|---|
| **Cancelación <24h → marca impuntualidad** | `CancelAppointmentAsync` calcula horas restantes, setea `IsLateCancellation` |
| **3 marcas → bloqueo reserva portal** | `Patient.LateCancellationCount >= 3` → `IsBlocked=true` |
| **Staff puede desbloquear** | `UnblockPatientAsync` (policy `CanUnblockPatients`) |
| **No-show → marca independiente** | `IsNoShow` + `Patient.NoShowCount++` |
| **Historial de cambios** | `AppointmentHistory` en cada cambio de estado/horario |

---

## API del Servicio

```csharp
public interface IAppointmentService
{
    Task<AppointmentResult> CreateAppointmentAsync(Appointment appointment, bool isPatientBooking = false);
    Task<AppointmentResult> UpdateAppointmentAsync(Appointment appointment, bool isPatientBooking = false);
    Task<AppointmentResult> CancelAppointmentAsync(Guid appointmentId, Guid cancelledByUserId, string reason, bool isPatientCancellation = false);
    Task<AppointmentResult> MarkNoShowAsync(Guid appointmentId, Guid markedByUserId);
    Task<AppointmentResult> UnblockPatientAsync(Guid patientId, Guid unblockedByUserId);
    Task<Appointment?> GetAppointmentAsync(Guid appointmentId);
    Task<List<Appointment>> GetAppointmentsByClinicAsync(Guid clinicId, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<Appointment>> GetAppointmentsByDoctorAsync(Guid doctorId, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<Appointment>> GetAppointmentsByPatientAsync(Guid patientId, DateTime? startDate = null, DateTime? endDate = null);
}

public class AppointmentResult
{
    public bool Success { get; set; }
    public Appointment? Appointment { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
```

---

## Registro en DI (Program.cs)

```csharp
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
```

---

## Tests (6 tests)

| Test | Qué Verifica |
|---|---|
| `CreateAppointmentAsync_ValidAppointment_ReturnsSuccess` | Creación exitosa con validación completa |
| `CreateAppointmentAsync_OverlappingAppointment_ReturnsFailure` | Rechaza traslapes |
| `CancelAppointmentAsync_LateCancellationByPatient_IncrementsLateCount` | Cancelación <24h incrementa contador |
| `CancelAppointmentAsync_ThirdLateCancellation_BlocksPatient` | 3ra cancelación tardía bloquea paciente |
| `MarkNoShowAsync_ValidAppointment_MarksNoShowAndIncrementsCount` | No-show incrementa contador independiente |
| `UnblockPatientAsync_BlockedPatient_UnblocksAndResetsCount` | Desbloqueo resetea contadores |
| `UpdateAppointmentAsync_Reschedule_ValidatesNewTime` | Reprogramación valida nuevo horario |

---

## Flujo de Estados de Cita

```
Scheduled → Confirmed → Completed
    ↓
CancelledByPatient (IsLateCancellation?) → LateCancellationCount++
    ↓
CancelledByStaff
    ↓
NoShow (IsNoShow=true) → NoShowCount++
```

---

## Integración con ScheduleService

- `CreateAppointmentAsync` → `ValidateAppointmentAsync` + `GenerateTimeSlotForAppointmentAsync`
- `UpdateAppointmentAsync` → `ValidateAppointmentAsync` (si cambió horario/doctor) + `RegenerateTimeSlotForAppointmentAsync`
- `CancelAppointmentAsync` / `MarkNoShowAsync` → `ReleaseTimeSlotForAppointmentAsync`

---

## Próximos Pasos (Fase 2 - Continuación)

1. **`SlotGenerator`** — Job programado (diario/semanal) que genera `TimeSlot` desde `DoctorSchedule`
2. **Festivos México 2026** — Integración con skill `puentesmx` para poblar `ClinicHoliday`
3. **Endpoints API** — Controllers Blazor/WebAPI para citas, slots, horarios
4. **UI Blazor** — Componentes de calendario, selector de slots, confirmación cita
5. **Patient Portal** — Flujo de reserva paciente: correo + código 6 dígitos (`PatientToken` tipo `AppointmentConfirmation`)

---

## Referencias

- `docs/PRD.md` — Requisitos completos Fase 2
- `docs/PRD_COMPLIANCE.md` — Checklist cumplimiento
- `docs/PLAN_PHASE1_EXECUTION.md` — Plan de ejecución
- `docs/SCHEDULE_SERVICE.md` — Servicio de validación de horarios
- `docs/ROLES_POLICIES.md` — Roles y políticas (CanUnblockPatients)

---

*Servicio listo para producción. Tests 45/45 pasando (incluye 6 tests AppointmentService). Build 0 errores.*