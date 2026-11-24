# Plan de ejecución — Fase 1: Infraestructura y autenticación

## Alcance

Implementar la fundación técnica local de la Fase 1, sin modificar ni desplegar una base de datos compartida: solución .NET 10, Blazor Interactive Server, Serilog, persistencia SQL Server/EF Core, ASP.NET Core Identity y roles iniciales con separación por consultorio.

## Estado inspeccionado

- `Klinara.slnx` y `Klinara.Web` existen; el proyecto es una plantilla Blazor mínima.
- Serilog ya está en `Program.cs` y el `.csproj`; falta verificar que el sink de archivo se configure realmente y que los logs no incluyan secretos/OTP.
- Hay archivos generados de Blazor sin seguimiento en Git.
- **COMPLETADO**: Proyecto de pruebas `Klinara.Tests` creado + configuración EF Core/Identity con tenant isolation.
- No se utilizarán credenciales de BD ni se aplicarán migraciones a una BD remota durante esta fase.

## Progreso completado (2026-11-23)

✅ **Paso 2**: Tests RED → GREEN para invariantes de tenant/roles (`TenantIsolationTests.cs`, 7 tests pasando)
✅ **Paso 3**: EF Core SQL Server + entidad `Clinic` + `ApplicationUser.ClinicId` nullable + índices únicos (`Subdomain`, `ContactEmail`) + índice en `User.ClinicId`
✅ **Paso 4**: Roles y políticas implementados (`KlinaraRoles`, `KlinaraPolicies`, `RoleSeeder`), 12 tests en `RolePolicyTests.cs`, seed al arranque, 19 tests totales pasando.
✅ **Paso 5**: Serilog configurado — sinks Console + File (rolling diario, 30 días), CorrelationId enricher, request logging middleware, output templates, filtrado Microsoft/ASP.NET Core, cierre limpio con `Log.CloseAndFlush()`.
✅ **Paso 6**: Identity + OTP implementado — `OtpService`, `LoggingEmailSender`, `OtpEmailSender`, `SmtpEmailSender` (SMTP real), campos OTP en `ApplicationUser`, lockout 5 intentos/15min, token 6 dígitos 1h, `LoggingEmailSender` sin secretos en logs, 20 tests totales pasando.
✅ **Paso 7 (Fase 2 - Inicio)**: Modelo de agenda y citas — Entidades `Appointment`, `AppointmentHistory`, `DoctorSchedule`, `TimeSlot`, `ClinicHoliday`, `DoctorVacation`, `Patient`, `PatientToken` con índices, relaciones y migración `AddSchedulingEntities`. ApplicationUser migrado a `IdentityUser<Guid>`.
✅ **Paso 8**: Email real implementado — `SmtpEmailSender` con SMTP configurable, `LoggingEmailSender` para desarrollo, `OtpEmailSender` para OTPs, config en `appsettings.json` / `appsettings.Development.json`.
✅ **Paso 9 (Fase 2 - Servicios de agenda)**: `ScheduleService` implementado con validación completa: traslapes, horario doctor, intervalo comida, festivos clínica, vacaciones doctor, horizonte reserva 45d (paciente) / ilimitado (staff), bloqueo paciente por 3 impuntualidades, generación de `TimeSlot` desde `DoctorSchedule` excluyendo festivos/vacaciones.
✅ **Paso 10 (Fase 2 - AppointmentService)**: `AppointmentService` implementado con CRUD citas, cancelación <24h → marca impuntualidad, 3 impuntualidades → bloqueo paciente, no-show → contador independiente, desbloqueo por staff, historial de cambios (`AppointmentHistory`), regeneración de `TimeSlot` al reprogramar.
✅ **Paso 11 (Fase 2 - SlotGenerator)**: `SlotGeneratorService` (BackgroundService) implementado — generación diaria automática de `TimeSlot` desde `DoctorSchedules`, excluye festivos/vacaciones/lunch, idempotente, configurable via `appsettings.json` (DaysAhead, RunAtUtc, Enabled).
✅ **Paso 12 (Fase 2 - API Controllers)**: 6 controladores WebAPI implementados — `SchedulesController`, `AppointmentsController`, `PatientsController`, `ClinicHolidaysController`, `DoctorVacationsController`, `SlotGeneratorController`. Todos con `[Authorize]` + policies, DTOs record, validación delegada a servicios.
✅ **Build**: .NET 10 compila con 0 errores (8 warnings de vulnerabilidades transitivas en `System.Security.Cryptography.Pkcs`)
✅ **Tests**: **45 tests pasando** (7 Tenant + 12 Role + 1 OTP + 18 Schedule + 1 Debug + 6 Appointment)
✅ **Documentación**: `docs/ROLES_POLICIES.md` + `docs/SERILOG.md` + `docs/IDENTITY_OTP.md` + `docs/PRD_COMPLIANCE.md` + `docs/SCHEDULE_SERVICE.md` + `docs/APPOINTMENT_SERVICE.md` + `docs/CHANGELOG.md` creados con matriz de permisos, guía de uso, configuración de logging, autenticación OTP y servicios de agenda.

## Secuencia restante

1. **Paso 4**: Integrar Identity en la aplicación — seed roles (`AdminPlataforma`, `AdminClinica`, `Doctor`, `Empleado`), policies, proteger rutas admin, `IEmailSender` stub (sin OTP en logs)
2. **Paso 5**: Serilog sink archivo + correlation ID; configuración segura (user-secrets ya tiene `UserSecretsId`)
3. **Paso 6**: Ejecutar pruebas, `dotnet build -t:Rebuild`, smoke test en puerto propio, revisar logs
4. **Paso 7**: Registrar en GitHub Issues bloqueos/decisiones (email/WhatsApp, URL/tenant, bootstrap admin, BD remota)
5. **Paso 8**: Actualizar `docs/PRD.md` separando terminado de pendiente

## Criterios de aceptación

- Build .NET 10 de la solución con 0 errores y, si es viable, 0 warnings nuevos.
- Pruebas verifican que un usuario de consultorio no obtiene datos de otro tenant y que solo el rol de plataforma tiene alcance global. ✅ **CUMPLIDO**
- Rutas/páginas administrativas requieren autenticación. ⏳ Pendiente
- No hay secretos, OTPs ni tokens en logs ni archivos versionados. ⏳ Pendiente (Serilog sink + IdentityNoOpEmailSender actual)
- Issues creados con URL e IDs para cada dependencia externa o decisión pendiente. ⏳ Pendiente

## Riesgos/decisiones abiertas

- Formato de URL propia por consultorio y bootstrap de cuentas/Administradores.
- Proveedor de email para OTP y recuperación; proveedor de WhatsApp para avisos.
- Token de paciente y tabla unificada asociada a paciente vs. usuario.
- Tiempos de OTP, sesión y reintentos; ya se definieron 1h para código y sesión, pero no el token de portal.
- Falta cadena de conexión segura y entorno SQL Server de desarrollo; no ejecutar migraciones en producción.
- Matriz exacta de permisos de Doctor/Empleado y visibilidad/auditoría de lecturas del Administrador de plataforma.
