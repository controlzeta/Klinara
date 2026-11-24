# Klinara — Confirmación, consulta y cancelación de citas

## Objetivo
Completar páginas funcionales de confirmación, consulta y cancelación, conectadas a `IPatientPortalService` con tokens de alcance por cita/paciente. Preservar modificaciones preexistentes en `.vs/*`, `nul`, OTP, docs y servicios de email.

## Estado final y verificación
- `PatientTokenType.AppointmentManagement` implementado para alcance por cita/paciente/clínica (45 días).
- `PatientPortalSessionState` implementado (Scoped) para pasar credenciales entre booking y vistas sin tokens sensibles en query string interno.
- `DoctorSelector.razor` y `SlotPicker.razor` completados y conectados al servicio.
- `VerifyEmail.razor` y `BookAppointment.razor` funcionales con selección de doctores, slots horizonte 45d, tipo de cita y notas.
- `ConfirmAppointment.razor`, `AppointmentView.razor` y `CancelAppointment.razor` implementados y cubiertos.
- 84 tests pasando (2 tests nuevos para AppointmentManagement y ciclo completo de reserva/consulta/cancelación).
- Compilación limpia: 0 errores, 0 advertencias en Klinara.Web.

## Fases
1. [x] Añadir pruebas de regresión del backend para crear/validar el token de gestión post-reserva y garantizar que sólo habilita su cita/paciente.
2. [x] Implementar servicio/state del portal para pasar token entre booking y páginas, sin exponerlo en la query string de navegación interna.
3. [x] Crear `ConfirmAppointment.razor` (consumir confirm token, mostrar resultado), `AppointmentView.razor` y `CancelAppointment.razor`; invocar servicios reales y manejar expiración/errores/loading.
4. [x] Completar selección/reserva si es requisito para llegar a estas páginas: cargar doctores/slots mediante `IPatientPortalService`, reservar con PortalAccess, redirigir a confirmación/resumen.
5. [x] Ejecutar pruebas dirigidas y suite completa, build; actualizar documentación.

## Criterios de aceptación
- [x] Reserva exitosa proporciona una credencial de cita con scope por cita/paciente/clínica.
- [x] Confirmación email consume únicamente el token de confirmación, con resultado claro.
- [x] Consulta no autoriza otras citas ni depende de un token ya consumido.
- [x] Cancelación usa el token de cita, cita objetivo explícita y reglas server-side.
- [x] No tokens sensibles en URL interna, logs ni documentación.
- [x] Tests y build pasan; limitaciones de entorno declaradas.
