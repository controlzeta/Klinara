# Klinara — Registro de Cambios y Estado del Proyecto

## Resumen Ejecutivo

SaaS multi-tenant para clínicas médicas en México. Fase 1 (infra + auth) y Fase 2 - Parte 1 (agenda/citas) completadas.

---

## Arquitectura

| Capa | Tecnología |
|---|---|
| **Framework** | .NET 10, Blazor Interactive Server |
| **BD** | SQL Server (LocalDB para dev, Azure/hosting para prod) |
| **ORM** | EF Core 10 con Identity |
| **Auth** | OTP 6 dígitos (email), lockout 5/15min, roles/policies |
| **Logging** | Serilog (Console + File rolling 30d, CorrelationId) |
| **Tests** | xUnit + InMemory DB (45 tests) |

---

## Entidades del Dominio

### Core (Identity + Multi-tenant)

| Entidad | Clave | Notas |
|---|---|---|
| `ApplicationUser` | `Guid` | `ClinicId?` nullable (null = PlatformAdmin) |
| `Clinic` | `Guid` | Subdomain/ContactEmail únicos, TimeZoneId |

### Agenda y Citas (Fase 2)

| Entidad | Clave | Relaciones |
|---|---|---|
| `DoctorSchedule` | `Guid` | Clinic, Doctor (User), DayOfWeek, horarios, lunch |
| `DoctorVacation` | `Guid` | Clinic, Doctor, rango fechas |
| `ClinicHoliday` | `Guid` | Clinic, fecha, recurrente, día completo/parcial |
| `Patient` | `Guid` | Clinic, CreatedByUser, contadores impuntualidad/no-show |
| `Appointment` | `Guid` | Clinic, Doctor, Patient, estado, tipo, flags |
| `AppointmentHistory` | `Guid` | Appointment, From/To status, reason, user |
| `TimeSlot` | `Guid` | Clinic, Doctor, rango, IsBooked, AppointmentId nullable |
| `PatientToken` | `Guid` | Patient, tipo (PortalAccess, AppointmentConfirmation...), token único |

---

## Servicios Implementados

### 1. `IScheduleService` / `ScheduleService`

**Validación completa de citas:**
- ✅ Clínica activa
- ✅ Doctor pertenece a clínica
- ✅ Paciente pertenece a clínica
- ✅ Horario válido (StartAt < EndAt, min 15 min)
- ✅ Sin traslapes mismo doctor
- ✅ Dentro de `DoctorSchedule` vigente
- ✅ Excluye `LunchStart`–`LunchEnd`
- ✅ Excluye `ClinicHoliday` (día completo o parcial)
- ✅ Excluye `DoctorVacation`
- ✅ Horizonte reserva: paciente 45 días / staff ilimitado
- ✅ Bloqueo paciente 3+ impuntualidades

**Generación de slots:**
- `GenerateSlotsForPeriodAsync(clinicId, doctorId, startDate, endDate)`
- Crea `TimeSlot` desde `DoctorSchedule` excluyendo festivos/vacaciones/lunch

### 2. `IAppointmentService` / `AppointmentService`

**CRUD citas:**
- `CreateAppointmentAsync` → valida + crea + genera TimeSlot
- `UpdateAppointmentAsync` → valida cambios + historial + regenera TimeSlot
- `CancelAppointmentAsync` → <24h = late cancellation, 3 = bloqueo paciente
- `MarkNoShowAsync` → contador independiente + libera slot
- `UnblockPatientAsync` → resetea contadores (policy CanUnblockPatients)
- Consultas por clínica/doctor/paciente con filtros de fecha

---

## Políticas de Autorización (`KlinaraPolicies`)

| Policy | Roles | Uso |
|---|---|---|
| `CanManageClinic` | AdminPlataforma, AdminClinica | Admin clínica |
| `CanManageUsers` | AdminPlataforma, AdminClinica | Usuarios |
| `CanManageSchedules` | AdminPlataforma, AdminClinica, Doctor | Horarios doctor |
| `CanManageAppointments` | AdminPlataforma, AdminClinica, Doctor | Citas |
| `CanViewReports` | AdminPlataforma, AdminClinica, Doctor | Reportes |
| `CanManagePatients` | AdminPlataforma, AdminClinica, Doctor | Pacientes |
| `CanAccessPatientPortal` | Paciente | Portal paciente |
| `CanUnblockPatients` | AdminPlataforma, AdminClinica | Desbloqueo impuntualidad |
| `CanManageHolidays` | AdminPlataforma, AdminClinica | Festivos clínica |
| `CanManageVacations` | AdminPlataforma, AdminClinica, Doctor | Vacaciones doctor |
| `IsPlatformAdmin` | AdminPlataforma | Acceso global |

---

## Cambios Recientes (Nov 2026)

### SlotGeneratorService (Fase 2 - Job automático)

**Nuevo servicio:** `SlotGeneratorService` (BackgroundService)

| Característica | Detalle |
|---|---|
| **Tipo** | `IHostedService` / `BackgroundService` |
| **Ejecución** | Diaria a hora configurable (default 03:00 UTC) |
| **Config** | `SlotGenerator` section en appsettings.json |
| **Días adelante** | 30 días (configurable) |
| **Idempotente** | Sí - verifica existencia antes de crear |

**Lógica de generación:**
1. Obtiene clínicas activas con `DoctorSchedules` activos
2. Agrupa por (ClinicId, DoctorId) → procesa todos los horarios de cada doctor
3. Para cada horario: itera días en rango efectivo
4. Excluye: día incorrecto, festivos día completo, vacaciones doctor, lunch, festivos parciales
5. Crea `TimeSlot` solo si no existe (idempotente)

**Configuración (appsettings.json):**
```json
"SlotGenerator": {
  "Enabled": true,
  "DaysAhead": 30,
  "RunAtUtc": "03:00:00"
}
```

**Deshabilitado en Development** (appsettings.Development.json):
```json
"SlotGenerator": { "Enabled": false, "DaysAhead": 7, "RunAtUtc": "03:00:00" }
```

### API Controllers (Fase 2 - Endpoints WebAPI)

**Nuevos controladores:**

| Controlador | Endpoints | Políticas |
|---|---|---|
| `SchedulesController` | CRUD `DoctorSchedule`, slots, disponibilidad | `CanManageSchedules`, `CanManageAppointments` |
| `AppointmentsController` | CRUD citas, cancelar, no-show, desbloquear paciente | `CanManageAppointments`, `CanManagePatients`, `CanUnblockPatients` |
| `PatientsController` | CRUD pacientes | `CanManagePatients` |
| `ClinicHolidaysController` | CRUD festivos clínica | `CanManageHolidays` |
| `DoctorVacationsController` | CRUD vacaciones doctor | `CanManageVacations` |
| `SlotGeneratorController` | Trigger manual generación slots | `CanManageSchedules` |

**Convenciones:**
- Base route: `/api/{controller}`
- Todos requieren `[Authorize]` + policy específica
- DTOs record para request/response
- Validación delegada a `ScheduleService` / `AppointmentService`

| Archivo | Cambio | Motivo |
|---|---|---|
| `ApplicationDbContext.cs` | `PatientToken.PatientId` → `OnDelete(NoAction)` | Evita ciclo: PatientToken→Patient(Cascade)→Clinic(Cascade) |
| `ApplicationDbContext.cs` | `TimeSlot.ClinicId` → `OnDelete(NoAction)` | Evita ciclo: TimeSlot→Clinic(Cascade)←DoctorSchedule |
| `SchedulingEntities.cs` | Eliminado `PatientToken.ClinicId` y navegación `Clinic` | Redundante (derivado de Patient) |

> **Nota**: Estos cambios son para `EnsureCreated()` en BD nuevas. La migración `AddSchedulingEntities` existente mantiene la estructura original para BD remota.

### Base de Datos Local

- **Instancia**: `(localdb)\MSSQLLocalDB` (SQL Server 2025 Express)
- **BD**: `Klinara_Test`
- **Tablas**: 16 (8 Identity + 8 Klinara)
- **Connection String**:
  ```json
  "Server=(localdb)\\MSSQLLocalDB;Database=Klinara_Test;Trusted_Connection=True;TrustServerCertificate=True"
  ```

---

## Tests (45/45 pasando)

| Suite | Tests | Cobertura |
|---|---|---|
| `TenantIsolationTests` | 7 | Aislamiento ClinicId, PlatformAdmin bypass, índices únicos |
| `RolePolicyTests` | 12 | Jerarquía roles, policies, seed |
| `OtpServiceTests` | 1 | Constantes OTP |
| `ScheduleServiceTests` | 18 | Validación citas, slots, festivos, vacaciones, lunch, horizonte |
| `ScheduleServiceDebugTests2` | 1 | Debug retrieval fechas futuras |
| `AppointmentServiceTests` | 6 | CRUD, cancelación tardía, bloqueo, no-show, desbloqueo, reprogramación |

**Ejecutar:**
```bash
cd /c/github/Klinara && dotnet test Klinara.Tests/Klinara.Tests.csproj --no-build
```

---

## Documentación Generada

| Archivo | Descripción |
|---|---|
| `docs/PRD.md` | Requisitos completos |
| `docs/PRD_COMPLIANCE.md` | Checklist cumplimiento |
| `docs/PLAN_PHASE1_EXECUTION.md` | Plan pasos 1-10 completados |
| `docs/ROLES_POLICIES.md` | Matriz roles/políticas |
| `docs/SERILOG.md` | Configuración logging |
| `docs/IDENTITY_OTP.md` | Autenticación OTP |
| `docs/SCHEDULE_SERVICE.md` | Servicio agenda |
| `docs/APPOINTMENT_SERVICE.md` | Servicio citas |

---

## Próximos Pasos (Fase 2 - Continuación)

1. **SlotGenerator** — Job programado (Hangfire/Quartz) que genera `TimeSlot` diario/semanal desde `DoctorSchedule`
2. **Festivos México 2026** — Integración skill `puentesmx` para poblar `ClinicHoliday`
3. **Endpoints API** — Controllers WebAPI/Blazor para citas, slots, horarios
4. **UI Blazor** — Calendario, selector slots, confirmación cita
5. **Patient Portal** — Flujo reserva: email + código 6 dígitos (`PatientToken` tipo `AppointmentConfirmation`)

---

## Comandos Útiles

```bash
# Build + tests
cd /c/github/Klinara && dotnet build && dotnet test Klinara.Tests/Klinara.Tests.csproj --no-build

# Crear BD local desde cero
cd /c/github/Klinara/InitLocalDb && dotnet run

# Conectar a BD local
sqlcmd -S "(localdb)\MSSQLLocalDB" -d Klinara_Test

# Ver migraciones
cd /c/github/Klinara && dotnet ef migrations list --project Klinara.Web
```