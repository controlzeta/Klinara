# Estado de cumplimiento del PRD — Klinara

**Corte de auditoría:** 2026-11-24  
**Base versionada:** `main` en `d970a88` (`v1.0-mvp-fase3`), sin commits adelantados/atrasados respecto a `origin/main`.  
**Alcance:** código versionado y cambios locales actuales. Los cambios locales de pagos y pantallas staff siguen sin commit y no forman parte del tag de lanzamiento.

### Estados
- ✅ Cumplido en código y evidencia revisada.
- 🟡 Parcial; existe implementación, pero faltan validación o criterios.
- ❌ Pendiente o incumplido.
- ⚠️ Bloqueante: existe código, pero no se debe contar como aceptado hasta resolverlo.

## Verificación ejecutada

| Verificación | Resultado |
|---|---|
| `dotnet test Klinara.slnx --no-restore` | ✅ 86/86 pruebas pasan; son pruebas xUnit. |
| `dotnet build Klinara.slnx --no-restore -t:Rebuild` en salida aislada | ✅ 0 errores; ⚠️ 45 advertencias nullable en `Klinara.Tests` (CS8604/CS8602). `Klinara.Web` compiló sin advertencias. |
| Pruebas de integración de controladores / Playwright | ❌ No hay proyecto ni pruebas de estos tipos en la solución. |
| Revisión de portal en navegador | ❌ No verificable: `http://localhost:5247/portal/demo` no respondió desde el navegador usado. No se inició otra instancia para evitar tocar el proceso/configuración local. |
| Estado del repositorio | ⚠️ Hay modificaciones y archivos sin seguimiento, incluidos archivos `.vs`, `nul` y trabajo de pagos/UI. No se descartaron ni se atribuyeron al tag. |

## Estado por fase

### Fase 1 — Fundamentos y autenticación: 🟡 Parcial

| Requisito | Estado / evidencia |
|---|---|
| .NET 10, Blazor Interactive Server, SQL Server/EF Core, Serilog | ✅ Configurados en `Klinara.Web.csproj` y `Program.cs`. |
| Roles y políticas base | ✅ Existen cuatro roles y políticas; falta asegurar su aplicación con aislamiento real por consultorio. |
| Login con correo + OTP de 6 dígitos | ❌ `Account/Login.razor` usa contraseña/passkey. El OTP está conectado al portal del paciente, no al login del personal. |
| Sesión revocable de 1 hora | 🟡 Hay lockout y rotación de security stamp; no se encontró configuración que fije expiración de cookie en 1 hora. |
| Recuperación por email / SMTP | 🟡 Hay `SmtpEmailSender`, pero la entrega real depende de configuración de producción y no se verificó contra un proveedor. |
| Aislamiento por clínica | ⚠️ No aceptado. Varias rutas reciben `clinicId`/`patientId` del cliente sin compararlos con el ámbito del usuario autenticado; algunas vistas también consultan por ID directamente. Los tests existentes simulan filtros y no prueban esas rutas/servicios. |

### Fase 2 — Agenda y citas: 🟡 Mayormente implementada, con brechas

| Requisito | Estado / evidencia |
|---|---|
| Modelos de cita, horario, slots, festivos y vacaciones | ✅ Presentes en `SchedulingEntities.cs`. |
| Validación de traslapes, horarios, comida, festivos y vacaciones | ✅ Implementada en `ScheduleService` / `AppointmentService`. |
| Anticipación de 24 horas y horizonte de 45 días para pacientes | ✅ Validada server-side; el personal no usa el límite del portal. |
| Cancelación tardía, contador, bloqueo a 3 y desbloqueo por personal | ✅ Lógica en `AppointmentService`; falta comprobar autorización y aislamiento por clínica extremo a extremo. |
| No-show independiente | ✅ Marca `IsNoShow` e incrementa `NoShowCount`, sin sumar impuntualidad. |
| Festivo parcial | ⚠️ `GenerateSlotsForPeriodAsync` trata cualquier festivo parcial como día completo; el generador en background sí filtra por intervalo. Hay resultados distintos entre ambas rutas. |
| Zona horaria de clínica | 🟡 Se almacena `Clinic.TimeZoneId` y middleware la expone, pero no se verificó conversión consistente al presentar/validar slots. |

### Fase 3 — Portal del paciente y reservas: ⚠️ Código presente; aceptación funcional bloqueada

| Requisito | Estado / evidencia |
|---|---|
| API pública, OTP por email, tokens y páginas de reserva/consulta/cancelación | ✅ Implementados en `PatientPortalController`, servicios y páginas Blazor. |
| Tokens de acceso/confirmación/gestión con expiración y alcance | ✅ Ciclo de creación y validación está implementado y cubierto parcialmente por pruebas unitarias. |
| Bloqueo y horizonte desde portal | ✅ Validaciones server-side en servicios de agenda/citas. |
| Persistencia de una cita pública en SQL Server | ⚠️ `PatientPortalService` asigna `CreatedByUserId = patient.Id`, pero esa columna referencia `ApplicationUser`; para el paciente del portal son IDs distintos. Las pruebas con EF InMemory no acreditan que la inserción pase las FK reales. |
| Enlace de confirmación enviado por correo | ⚠️ La URL construida no incluye el subdominio requerido por la ruta Blazor `/portal/{subdomain}/confirm/{token}`. El flujo con correo real no está validado. |
| No exposición de tokens | 🟡 La navegación conserva el token de gestión en la ruta; los tokens son credenciales portadoras y la revisión no verificó políticas de referrer, telemetría ni acceso a logs en producción. |
| Prueba completa de flujo público | ❌ Sin pruebas de integración de controlador ni Playwright; portal no se pudo abrir en navegador durante esta auditoría. |

### Fase 4 — Pagos y reportes: 🟡 Trabajo local en progreso; no listo para release

| Requisito | Estado / evidencia |
|---|---|
| Entidad, servicio, API, pantalla staff y pruebas unitarias | 🟡 Existen en el árbol de trabajo local (`Payment.cs`, `IPaymentService.cs`, `PaymentsController.cs`, `StaffPayments.razor`, `PaymentServiceTests.cs`); no están incluidos en el tag y siguen sin commit. |
| Registro de monto, fecha, método y concepto; saldo adeudado menos abonos | 🟡 El modelo/servicio cubren estos campos y cálculo. La pantalla y las pruebas actuales no sustituyen la persistencia y autorización de producción. |
| Migración EF / tabla SQL | ❌ No se encontró migración para `Payment` ni actualización del model snapshot; la base real no puede darse por lista para este módulo. |
| Aislamiento por clínica y autorización de recursos | ⚠️ La API permite consultar balance por `patientId` o pagos por `clinicId` sin comprobar que coincidan con la clínica del usuario; debe resolverse antes de habilitarla. |
| Reportes | 🟡 Hay listado de pagos por clínica y resumen de saldo por paciente; no hay exportación ni evidencia de reporte validado en UI/runtime. |

### Fase 5 — Privacidad y cumplimiento: ❌ Pendiente

- No se encontró aviso de privacidad final aprobado por asesoría jurídica ni flujo/procedimiento ARCO implementado.
- No se almacena un expediente clínico como módulo; sin embargo, `Patient.Notes` y otros datos personales requieren controles y revisión de privacidad. No equivale a cumplimiento legal.

### Fase 6 — Notificaciones: 🟡 Parcial

- Existe código SMTP, pero su uso depende de configuración de producción; la entrega no se probó.
- ❌ No se encontró proveedor ni integración de WhatsApp, plantillas completas, ni envío programado de recordatorios/alertas.
- ⚠️ El correo de confirmación usa la plantilla genérica de confirmación de cuenta y el enlace de portal no coincide con la ruta pública.

### Fase 7 — UI y UX: 🟡 En progreso local; no aceptada

- El árbol de trabajo contiene cambios en `Home.razor`, `NavMenu.razor` y páginas staff de pacientes, citas y pagos. Son cambios locales no incluidos en el tag.
- El documento de diseño indica que el sistema visual aún no está integrado/validado en todas las pantallas. No hay evidencia de revisión visual, accesibilidad, tema oscuro ni pruebas móviles en navegador.
- `Components/App.razor` conserva `lang="en"`; el PRD requiere interfaz en español.

### Fase 8 — Pruebas y lanzamiento: 🟡 Parcial

| Requisito | Estado / evidencia |
|---|---|
| Suite automatizada | ✅ 86/86 pruebas xUnit pasan en la ejecución auditada. No son pruebas de controlador ni de navegador. |
| Build sin errores | ✅ 0 errores en rebuild aislado. |
| Cero advertencias | ❌ Rebuild de solución reportó 45 advertencias nullable en el proyecto de pruebas. |
| Playwright 75% en flujos críticos | ❌ Sin Playwright ni denominador/medición de cobertura; no se cumple la meta. |
| Runtime, CI, deploy y checklist final | ❌ No verificados en esta auditoría; el tag `v1.0-mvp-fase3` existe, pero no demuestra aceptación del PRD completo. |

## Flujos de aceptación del MVP

| Flujo / criterio | Estado actualizado |
|---|---|
| Flujo 1: login → alta paciente → reserva → pago → dashboard | ❌ Incompleto: login por contraseña, reserva pública tiene bloqueo potencial por FK, pagos están en WIP sin migración y UI/dashboard no están validados. |
| Flujo 2: cancelación <24h → marca → bloqueo a 3 | 🟡 Regla principal implementada; aislamiento/autorización y recorrido completo no probados. |
| Flujo 3: no-show independiente | 🟡 Servicio/modelo implementados; recorrido completo no probado. |
| No funcionales: build, zona horaria, mobile-first | 🟡 Build sin errores, pero con 45 warnings; zona horaria y móvil sin validación de runtime. |
| Playwright ≥75% | ❌ No iniciado. |

## Prioridades para cerrar brechas

1. Corregir y probar aislamiento por consultorio en cada controller/servicio/página; dejar de confiar en IDs recibidos del cliente.
2. Desbloquear el flujo público con SQL Server real: `CreatedByUserId` válido, enlace de confirmación con la ruta/subdominio correctos y pruebas de integración del ciclo completo.
3. Completar Fase 4 antes de contarla como entregada: migración EF, autorización por clínica y pruebas de API/persistencia.
4. Decidir e implementar login OTP y expiración de sesión de una hora según el PRD.
5. Unificar tratamiento de festivos parciales y conversión de zona horaria.
6. Seleccionar/probar proveedor de email y WhatsApp; agregar confirmaciones y recordatorios requeridos.
7. Integrar el sistema visual, revisar español/accesibilidad/mobile-first y añadir Playwright/CI; corregir las advertencias nullable de tests.

---

*Este checklist refleja evidencia de código y ejecuciones disponibles en la fecha indicada; las casillas de implementación no sustituyen validación de runtime ni aceptación de producto.*
