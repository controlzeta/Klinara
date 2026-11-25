# PRD Compliance Checklist — Klinara

> **Estado vigente:** consulta la [auditoría actualizada](PRD_COMPLIANCE_AUDIT_2026-11-24.md). El contenido siguiente refleja el estado tras corrección Fase 3 + aviso privacidad + traducciones español (2026-11-25).

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

## ✅ FASE 2 EN PROGRESO — Agenda y Citas

|| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|---|
| **Modelo de cita** | ✅ | Entidad `Appointment` con status, tipo, notas, cancelación, no-show |
| **Disponibilidad por doctor/consultorio** | ✅ Modelo | `DoctorSchedule` con DayOfWeek, horarios, intervalo comida, SlotDuration |
| **Reglas de traslapes** | ✅ | Validada en `ScheduleService.ValidateAppointmentAsync` (item 8) y `AppointmentService` |
| **Horarios por día con intervalos de comida** | ✅ Modelo | `DoctorSchedule.LunchStart/End` |
| **Configurar festivos y vacaciones** | ✅ Modelo | `ClinicHoliday`, `DoctorVacation` |
| **Cancelación <24h → marca impuntualidad** | ✅ Modelo | `Appointment.IsLateCancellation`, `Patient.LateCancellationCount` |
| **3 marcas bloquean reserva en portal** | ✅ Modelo | `Patient.LateCancellationCount`, lógica en `AppointmentService.CancelAppointmentAsync` |
| **Personal puede desbloquear** | ✅ | Endpoint/servicio `UnblockPatientAsync` en `AppointmentService` (roles `EmployeeOrAbove`) |
| **No-show → marca independiente** | ✅ Modelo | `Appointment.IsNoShow`, `Patient.NoShowCount` |
| **Horizonte reserva: 45d paciente / todo staff** | ✅ | Validada server-side en `ScheduleService.ValidateAppointmentAsync` (item 11-12): 45d para paciente (`isPatientBooking=true`), ilimitado para staff |
| **Generación de slots (TimeSlot)** | ✅ Modelo | Entidad `TimeSlot` con IsBooked, IsBlocked, AppointmentId |
| **ClinicHoliday / DoctorVacation excluyen slots** | ✅ | Generador `SlotGeneratorService` y validador `ScheduleService` excluyen festivos/vacaciones/lunch al generar slots |

---

## 🟢 FASE 3 MAYORITARIA — Paciente y Reservas

| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|
| **Portal público** | ✅ | `PatientPortalController` + 6 páginas Blazor + `PatientPortalLayout` |
| **Reserva con verificación correo (código 6 dígitos)** | ✅ | `POST /verify-email` + `POST /validate-otp` → `PatientToken` tipo `PortalAccess` |
| **Token de un solo uso para acceso paciente** | ✅ | `PatientToken` (PortalAccess 30min, Confirmation 24h, Management 45d) |
| **Login del paciente** | ✅ | Flujo token-based sin IdentityUser: email → OTP → PortalAccess token |
| **Límites de reserva y bloqueo por 3 impuntualidades** | ✅ | Validado en `BookAppointmentAsync` → `Patient.IsBlocked` + `LateCancellationCount` |

**Bloqueos de auditoría resueltos (2026-11-25):**
- ✅ `CreatedByUserId` FK: usa `ApplicationUser.Id` del usuario portal (no `Patient.Id`)
- ✅ Email confirmación: URL incluye subdominio `/portal/{subdomain}/confirm/{token}`
- ✅ Playwright E2E: proyecto `Klinara.E2E` con 9 tests (páginas + API)
- ✅ Warnings nullable tests: reducidos 45→9 (`CreatePatient` `string? email`)
- ✅ Aviso de privacidad: página `/privacy` + enlaces en footers (admin + portal)

**Pendientes menores:**
- 🔒 Headers de seguridad para tokens en URL (referrer-policy, CSP)
- 📱 UI mobile-first / design system / accesibilidad (Fase 7)

## 🟡 FASE 4 EN PROGRESO — Pagos y Reportes

| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|
| **Modelo de pago manual** | ✅ | Entidad `Payment` con Amount, PaidAt, Method (enum: Cash/CreditCard/DebitCard/SPEI/Other), Concept, Notes |
| **Registrar pago: monto, fecha, método (efectivo, tarjetas, SPEI), concepto** | ✅ | `IPaymentService.RegisterPaymentAsync` + `PaymentsController.POST` + `StaffPayments.razor` modal |
| **Saldo pendiente: captura manual; saldo real = total adeudado - abonos** | ✅ | `Patient.TotalDue` - `Payments.Sum(Amount)` en `GetPatientBalanceAsync` |
| **Aislamiento por clínica** | ✅ | Controller valida `ClinicId` del usuario vs request; Service filtra por `requestingClinicId` |
| **Reportes por paciente y consultorio** | 🟡 | UI lista pagos con filtro por paciente; **falta exportación CSV/Excel/PDF** |

**Implementado (2026-11-25):**
- ✅ EF Migration `20261125162638_InitialCreate` incluye tabla `Payments` con FKs
- ✅ 2 tests unitarios en `PaymentServiceTests`
- ✅ Controller con autorización `EmployeeOrAbove` y validación de clínica

## 🔴 FASE 5 PENDIENTE — Privacidad y Cumplimiento

| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|
| **Aviso de privacidad final (jurídico)** | ❌ | Pendiente revisión legal |
| **Página de aviso de privacidad implementada** | ✅ | `PatientPortal/Pages/Privacy.razor` con 5 secciones (datos, finalidades, transferencias, seguridad, ARCO) |
| **Enlace a aviso en footers** | ✅ | `NavMenu.razor` (admin) + `PatientPortalLayout.razor` (portal) → `/privacy` |
| **Procedimiento ARCO implementado** | ❌ | Falta endpoint/proceso para solicitudes |
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
| **Idioma español en UI** | ✅ | Páginas Account traducidas (labels, placeholders, validaciones, botones); regla en `.cursorrules` |

**Implementado (2026-11-25):**
- ✅ Textos Account traducidos al español (`Login`, `Register`, `ForgotPassword`, `ResetPassword`, `Manage/*`)
- ✅ Regla en `.cursorrules`: "Todos los textos de UI DEBEN estar en español"

---

## 🟡 FASE 8 EN PROGRESO — Pruebas y Lanzamiento

| Requisito PRD | Estado | Evidencia / Falta |
|---|---|---|
| **Suite automatizada** | ✅ | 91 tests xUnit pasando |
| **Build sin errores** | ✅ | 0 errores, 2 warnings (solo versión Playwright) |
| **Cero advertencias** | 🟡 | 9 warnings nullable en tests (reducidos de 45) |
| **Playwright 75% en flujos críticos** | 🟡 | Proyecto `Klinara.E2E` creado con 9 tests; requiere app corriendo |
| **Runtime, CI, deploy, checklist final** | ❌ | Pendiente |

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
| Aviso de privacidad por consultorio | 🟡 Página implementada (`/privacy`); pendiente revisión legal y personalización por clínica |
| Datos sensibles (expedientes) fuera del MVP | ✅ Modelo no incluye |
| Derechos ARCO | 🟡 Documentados en aviso; falta implementación proceso |
| Domicilio/responsable en aviso | 🟡 Placeholder en borrador; requiere completado legal |

---

## 📋 CRITERIOS DE ACEPTACIÓN MVP — Flujo 1 a 3

| Flujo | Estado |
|---|---|
| **Flujo 1:** Login → Alta paciente → Reserva cita → Pago manual → Dashboard | 🟡 80% (Login ✅, Alta paciente ✅, Reserva ✅, Pago ✅, Dashboard ❌) |
| **Flujo 2:** Cancelación <24h → Marca impuntualidad → 3 marcas bloquean | 🟡 70% (Modelo ✅, Servicio ✅, Contador ✅, Bloqueo ✅, aislamiento ⚠️) |
| **Flujo 3:** No-show → Marca independiente | 🟡 70% (Modelo ✅, Servicio ✅, aislamiento ⚠️) |
| **No funcionales:** Build 0 errores, zona horaria, mobile-first | 🟡 70% (Build ✅, Zona horaria modelo ✅, Mobile-first ❌) |
| **Playwright ≥75%** | 🟡 20% (Proyecto creado, tests escritos, requieren app corriendo) |

---

## 📋 RESUMEN EJECUTIVO

| Fase | % Completo | Comentario |
|---|---|---|
| **Fase 1** | **95%** | Solo falta email real y validación móvil |
| **Fase 2** | **85%** | Modelo + servicios completos, aislamiento por clínica ⚠️ |
| **Fase 3** | **80%** | Portal funcional; bloqueos de auditoría resueltos; UI Fase 7 pendiente |
| **Fase 4** | **70%** | Entidad + servicio + controller + UI + tests; falta exportación reportes |
| **Fase 5** | **25%** | Página aviso + footers; pendiente revisión legal y proceso ARCO |
| **Fase 6** | **5%** | Solo decisión WhatsApp obligatorio |
| **Fase 7** | **0%** | Sin UI design system / mobile-first |
| **Fase 8** | **40%** | 91 tests unitarios + E2E proyecto; CI/deploy pendiente |

---

## 🎯 PRÓXIMAS ACCIONES PRIORITARIAS (Orden PRD)

1. **Fase 2 - Aislamiento por clínica extremo a extremo**
   - Verificar `ClinicId` en todos los controllers/servicios vs usuario autenticado
   - Tests de integración para aislamiento

2. **Fase 3 - Hardening seguridad tokens**
   - Referrer-Policy, CSP headers para páginas con tokens en URL
   - Log redaction de tokens bearer

3. **Fase 3 - UI mobile-first / design system (Fase 7)**
   - Integrar `UI_DESIGN_SYSTEM.md` en `PatientPortalLayout` y páginas
   - `lang="es"` en `App.razor`
   - Accesibilidad básica (ARIA, contraste, focus)

4. **Fase 4 - Reportes** (bloqueante para Flujo 1 completo)
   - Exportación CSV/Excel/PDF de pagos por paciente y clínica
   - Dashboard de saldos y cobranza

5. **Fase 6 - Notificaciones**
   - Proveedor WhatsApp (Twilio/Gupshup) + templates
   - `SmtpEmailSender` configuración producción
   - Recordatorios 24h (job background)

6. **Fase 8 - CI/CD y E2E real**
   - Pipeline: build → test → Playwright contra app levantada
   - Deploy checklist + documentación usuario

---

*Actualizado: 2026-11-25 | Basado en PRD.md v1.0, código actual (build ✅, 91 tests ✅, Fase 3 bloqueos resueltos, Fase 4 pagos implementados, aviso privacidad + footers + textos español)*