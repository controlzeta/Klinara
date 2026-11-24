# PRD Compliance Checklist — Klinara

## Estado actual vs PRD (2026-11-23)

---

## ✅ FASE 1 COMPLETA — Infraestructura y Auth

| Requisito PRD | Estado | Evidencia |
|---|---|---|
| **.NET 10 LTS** | ✅ | `TargetFramework>net10.0` |
| **Blazor Interactive Server** | ✅ | `AddInteractiveServerComponents()` |
| **Serilog** | ✅ | Dual sink Console+File, CorrelationId, request logging |
| **SQL Server / EF Core** | ✅ | `Microsoft.EntityFrameworkCore.SqlServer` |
| **Auth OTP 6 dígitos 1h** | ✅ | `OtpService`, `LoggingEmailSender`, `OtpEmailSender`, `SmtpEmailSender` |
| **Recuperación por correo** | ✅ | `OtpPurposes.PasswordReset` + `SmtpEmailSender` implementado |
| **Sesión 1h revocable** | ✅ | `Lockout` 5/15min, `SecurityStamp` rotation |
| **Roles y permisos** | ✅ | 4 roles, 11 políticas, seed automático |
| **Aislamiento por clínica (ClinicId)** | ✅ | `ClinicId` en `ApplicationUser`, queries filtradas, 7 tests |
| **Admin plataforma (ClinicId=null)** | ✅ | Bypass global en policies y queries |
| **Build 0 errores** | ✅ | Solo 8 warnings de vulnerabilidades transitivas |

---

## 🟡 FASE 2 EN PROGRESO — Agenda y Citas

| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|
| **Modelo de cita** | ✅ | Entidad `Appointment` con status, tipo, notas, cancelación, no-show |
| **Disponibilidad por doctor/consultorio** | ✅ Modelo | `DoctorSchedule` con DayOfWeek, horarios, intervalo comida, SlotDuration |
| **Reglas de traslapes** | ❌ | Falta servicio de validación |
| **Horarios por día con intervalos de comida** | ✅ Modelo | `DoctorSchedule.LunchStart/End` |
| **Configurar festivos y vacaciones** | ✅ Modelo | `ClinicHoliday`, `DoctorVacation` |
| **Cancelación <24h → marca impuntualidad** | ✅ Modelo | `Appointment.IsLateCancellation`, `Patient.LateCancellationCount` |
| **3 marcas bloquean reserva en portal** | ✅ Modelo | `Patient.LateCancellationCount`, lógica pendiente en servicio |
| **Personal puede desbloquear** | ❌ | Falta endpoint/servicio `UnblockPatient` |
| **No-show → marca independiente** | ✅ Modelo | `Appointment.IsNoShow`, `Patient.NoShowCount` |
| **Horizonte reserva: 45d paciente / todo staff** | ❌ | Falta validación en servicio de reserva |
| **Generación de slots (TimeSlot)** | ✅ Modelo | Entidad `TimeSlot` con IsBooked, IsBlocked, AppointmentId |
| **ClinicHoliday / DoctorVacation excluyen slots** | ❌ | Falta generador de slots que considere estos |

---

## 🔴 FASE 3 PENDIENTE — Paciente y Reservas

| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|
| **Portal público** | ❌ | Solo modelo `Patient`, `PatientToken` |
| **Reserva con verificación correo (código 6 dígitos)** | ⚠️ Modelo | `PatientTokenType.AppointmentConfirmation`, `EmailVerification` |
| **Token de un solo uso para acceso paciente** | ✅ Modelo | `PatientToken` con Token unique, ExpiresAt, UsedAt, Type=PortalAccess |
| **Login del paciente** | ❌ | Falta flujo auth separado para pacientes (sin IdentityUser) |
| **Límites de reserva y bloqueo por 3 impuntualidades** | ⚠️ Modelo | `Patient.LateCancellationCount`, `IsBlocked`, `BlockedUntil` |

---

## 🔴 FASE 4 PENDIENTE — Pagos y Reportes

| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|
| **Modelo de pago manual** | ❌ | No hay entidad `Payment` |
| **Registrar pago: monto, fecha, método (efectivo, tarjetas, SPEI), concepto** | ❌ | |
| **Saldo pendiente: captura manual; saldo real = total adeudado - abonos** | ❌ | |
| **Reportes por paciente y consultorio** | ❌ | |

---

## 🔴 FASE 5 PENDIENTE — Privacidad y Cumplimiento

| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|
| **Aviso de privacidad final (jurídico)** | ❌ | Pendiente |
| **Procedimiento ARCO implementado** | ❌ | |
| **Datos sensibles revisados** | ❌ | MVP no almacena expedientes clínicos (OK) |
| **Datos clínicos fuera del MVP** | ✅ | Modelo no incluye expedientes |

---

## 🔴 FASE 6 PENDIENTE — Notificaciones

| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|
| **WhatsApp obligatorio** | ❌ | Proveedor pendiente (Twilio/Gupshup) |
| **Email cuando esté disponible** | ⚠️ | Stub `LoggingEmailSender`, falta implementación real |
| **Plantillas confirmación/alertas** | ❌ | |

---

## 🔴 FASE 7 PENDIENTE — UI y UX

| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|
| **Componentes Blazor** | ❌ | Solo plantilla base |
| **Diseño mobile-first** | ❌ | |
| **Mockup conectado a datos reales** | ❌ | |
| **Accesibilidad básica** | ❌ | |
| **Navegación entre módulos** | ❌ | |

---

## 🔴 FASE 8 PENDIENTE — Pruebas y Lanzamiento

| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|
| **Playwright 75% cobertura** | ❌ | Solo xUnit (20 tests unitarios) |
| **Build limpio 0 errores/0 warnings** | ⚠️ | 8 warnings vulnerabilidades transitivas |
| **Pruebas de runtime** | ❌ | |
| **Documentación final** | ⚠️ | Docs técnicas creadas, falta docs usuario |
| **Deploy y checklist lanzamiento** | ❌ | |

---

## 📋 DECISIONES TÉCNICAS CRÍTICAS — Verificación

| Decisión PRD | Implementado |
|---|---|
| .NET 10 LTS | ✅ |
| Blazor Interactive Server | ✅ |
| SQL Server | ✅ |
| Zona horaria por consultorio | ✅ Modelo (`Clinic.TimeZoneId`) |
| Mobile-first | ❌ Pendiente UI |
| Playwright 75% | ❌ Pendiente |

---

## 📋 CUMPLIMIENTO MÉXICO (LFPPP)

| Requisito | Estado |
|---|---|
| Aviso de privacidad por consultorio | ❌ Pendiente |
| Datos sensibles (expedientes) fuera del MVP | ✅ Modelo no incluye |
| Derechos ARCO | ❌ Pendiente |
| Domicilio/responsable en aviso | ❌ Pendiente |

---

## 📋 CRITERIOS DE ACEPTACIÓN MVP — Flujo 1 a 3

| Flujo | Estado |
|---|---|
| **Flujo 1:** Login → Alta paciente → Reserva cita → Pago manual → Dashboard | 🟡 20% (Login ✅, Alta paciente modelo ✅, Reserva ❌, Pago ❌, Dashboard ❌) |
| **Flujo 2:** Cancelación <24h → Marca impuntualidad → 3 marcas bloquean | 🟡 40% (Modelo ✅, Servicio cancelación ❌, Contador ✅, Bloqueo ❌) |
| **Flujo 3:** No-show → Marca independiente | 🟡 50% (Modelo ✅, Servicio no-show ❌) |
| **No funcionales:** Build 0 errores, zona horaria, mobile-first | 🟡 60% (Build ✅, Zona horaria modelo ✅, Mobile-first ❌) |
| **Playwright 75%** | ❌ 0% |

---

## 📋 RESUMEN EJECUTIVO

| Fase | % Completo | Comentario |
|---|---|---|
| **Fase 1** | **95%** | Solo falta email real y validación móvil |
| **Fase 2** | **40%** | Modelo completo, faltan servicios de negocio |
| **Fase 3** | **15%** | Solo modelos |
| **Fase 4** | **0%** | Sin entidad Payment |
| **Fase 5** | **10%** | Solo decisión de no expedientes |
| **Fase 6** | **5%** | Solo decisión WhatsApp obligatorio |
| **Fase 7** | **0%** | Sin UI |
| **Fase 8** | **5%** | Solo tests unitarios |

---

## 🎯 PRÓXIMAS ACCIONES PRIORITARIAS (Orden PRD)

1. **Fase 2 - Servicios de agenda** (`AppointmentService`, `ScheduleService`, `SlotGenerator`)
   - Validación traslapes, horizonte 45d, exclusión festivos/vacaciones
   - Generador de `TimeSlot` desde `DoctorSchedule` excluyendo `ClinicHoliday`/`DoctorVacation`

2. **Fase 2 - Cancelación y No-show**
   - `CancelAppointmentAsync` con validación <24h → `IsLateCancellation` + `Patient.LateCancellationCount++`
   - `MarkNoShowAsync` → `IsNoShow` + `Patient.NoShowCount++`
   - `UnblockPatientAsync` (solo roles `EmployeeOrAbove`)

3. **Fase 2 - Generador de slots**
   - Job semanal/diario que crea `TimeSlot` desde `DoctorSchedule`
   - Excluye `ClinicHoliday`, `DoctorVacation`, slots ya reservados/bloqueados

4. **Fase 3 - Portal paciente**
   - Endpoint público `/patient/booking` con verificación email (OTP)
   - `PatientToken` tipo `PortalAccess` / `AppointmentConfirmation`
   - Login paciente sin IdentityUser (token-based)

5. **Fase 4 - Pagos**
   - Entidad `Payment` + `PaymentMethod` enum
   - `Patient.Balance` computado (total - abonos)

6. **Fase 6 - Notificaciones**
   - Seleccionar proveedor WhatsApp (Twilio/Gupshup)
   - Implementar `IEmailSender` real (SendGrid/SMTP)
   - Plantillas: confirmación, recordatorio 24h, alerta cancelación

---

*Generado: 2026-11-23 | Basado en PRD.md v1.0 y código actual*