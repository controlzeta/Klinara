# Klinara — Product Requirements Document

## Phase 1: Fundamentos y alcance del MVP (Nov 2026)

### 1. Objective & Modelo de Servicio

**Producto:** Klinara — SaaS para administrar consultorios médicos independientes.

**Modelo:** Cada cuenta = un consultorio con URL propia. Aislamiento de datos por consultorio mediante validación de permisos en el servidor y la URL. El administrador de plataforma gestiona todos los consultorios; el administrador de clínica gestiona solo el suyo.

**Problema resuelto:** Simplificar la operación diaria de consultorios mediante la gestión de usuarios, pacientes, citas, pagos y comunicación.

### 2. MVP vs Post-MVP (Explicit List)

**ENTRA en el MVP (v1.0):**

1. **Autenticación:** Login con correo + código OTP de 6 dígitos (1h vigencia). Recuperación por correo. Sesión de 1h revocable.
2. **Dashboard mockup:** Datos ficticios para validar layout; conectar datos reales después.
3. **Gestión de citas (básico):** 
   - Agendar citas (personal y paciente con verificación de correo).
   - Cancelación tardía: <24h → marca de impuntualidad; 3 marcas bloquean reserva en portal; personal puede desbloquear.
   - No-show: marca independiente, no cuenta para impuntualidad.
   - Reserva del paciente con correo obligatorio y código de verificación de 6 dígitos.
   - Enlace/token de un solo uso para acceso del paciente; vencimiento, alcance y vínculo a paciente/usuario pendientes de definir.
   - Anticipación: mínimo 24 horas y máximo 45 días para el paciente; el personal ve todo el calendario y puede reservar fuera de ese rango.
4. **Gestión de pagos manuales:** 
   - Registrar pago: monto, fecha, método (efectivo, tarjetas, SPEI), concepto.
   - Saldo pendiente: captura manual; mostrar saldo real = total adeudado - abonos registrados.
   - Reportes por paciente y consultorio.
5. **Notificaciones de cita:** confirmación/alertas por WhatsApp obligatorias y por email cuando esté disponible; selección e integración del proveedor forman parte del MVP y son un prerrequisito de implementación.

### 3. Roles y Permisos (Finalizados)

| Rol | Permisos concretos |
|-----|-------------------|
| **Administrador de clínica** | Uno por consultorio; administra la cuenta y usuarios de su consultorio. Puede ver/modificar datos de su consultorio solo. |
| **Administrador de plataforma** | Puede ver y modificar todos los datos y administrar todos los consultorios. Acciones privilegiadas auditarse. |
| **Doctor** | Accede solo a su consultorio; puede dar de alta empleados y realizar funciones del Empleado. |
| **Empleado** | Administra y consulta citas y pagos. Puede desbloquear pacientes tras 3 marcas. |

### 4. Decisiones Técnicas Críticas

- **Framework:** .NET 10 LTS (soporte activo; .NET 9 soporte terminó mayo 2026).
- **Frontend:** Blazor Web App Interactive Server.
- **Base de datos:** SQL Server (por requerimiento del usuario).
- **Zona horaria:** Configurar por consultorio y presentar citas en esa zona.
- **Enfoque:** Mobile-first en el diseño de componentes Blazor.
- **Sistema visual:** ver `docs/UI_DESIGN_SYSTEM.md`; el plan de rediseño completo está en `docs/PLAN_UI_REDESIGN.md`.
- **Pruebas:** Suite Playwright con meta 75% cobertura; flujos críticos en CI antes de liberar.

### 5. Consideraciones de Cumplimiento (México)

- **Aviso de privacidad:** Borrador por consultorio (ver `Klinara.md` §64-80). Requiere revisión jurídica antes de publicar. Include identidad/domicilio del responsable, datos tratados, finalidades, opciones para limitar uso, mecanismos ARCO.
- **Datos sensibles:** El MVP no almacena expedientes clínicos. Datos de salud considerados sensibles por LFPPP requieren revisión jurídica antes de producción.
- **Derechos ARCO:** Procedimiento por definir; pending en aviso.

### 6. Decision Log (Ejemplos)

```markdown
- Decisión: .NET 10 LTS en lugar de .NET 9
- Contexto: .NET 9 soporte terminó mayo 2026; proyecto nuevo necesita soporte activo
- Alternativas: .NET 8 LTS (soporta hasta 2027), posponer decisión
- Pendiente: validar compatibilidad de bibliotecas

- Decisión: CORREO único por consultorio, no global
- Contexto: Permitir que el mismo paciente use su correo en distintas clínicas sin bloqueo
- Alternativa: correo global (descartado por limitar casos de uso)
```

### 7. Criterios de Aceptación para MVP

- **Flujo 1:** Login → Alta paciente → Reserva cita → Pago manual → Dashboard.
- **Flujo 2:** Cancelación <24h → Marca de impuntualidad → 3 marcas bloquean reserva portal.
- **Flujo 3:** No-show → Marca independiente en historial.
- **No funcionales:** Build 0 errores/0 warnings; zona horaria operative; diseño mobile-first comprobado en browser.
- **Cobertura Playwright:** Mínimo 75% en flujos: login, alta paciente, reserva cita, registro abono, confirmación cita.

### 8. Pendientes para completar el desarrollo

- Definir detalle de calendario por consultorio: horarios, festivos, vacaciones, intervalos de comida.
- Configurar proveedor de WhatsApp/SMS (investigar, probar, seleccionar) y credenciales.
- Implementar token de paciente: tabla unificada, tipos, vencimiento, revocación; vinculación al usuario sin cuenta propia.
- Definir y mapear saldo real vs saldo registrado; cálculo: total adeudado - abonos (ver definición en módulo pagos).
- Generar aviso de privacidad final con firma jurídica; completar responsables, proveedores, conservación y procedimiento ARCO.
- Mapear y validar roles: permisos concretos por rol y confirmar que el admin de clínica no ve datos ajenos.
- Añadir logs Serilog con correlation ID en flujos críticos.
- Definir y priorizar 8 fases de desarrollo (ver sección "Fases de desarrollo" al final).
- Criterios de aceptación finales para liberar v1.0.

--- ### Fases de desarrollo (roadmap)

**Fase 0 - Definición y decisión:** Ya definido en este PRD. Confirmar versión .NET, decisiones de arquitectura y MVP vs post-MVP.

**Fase 1 - Infraestructura y auth:** Configurar proyecto .NET 10, Solución, Blazor, Serilog, autenticación con OTP y sesión, roles y permisos en base de datos, URLs por consultorio, migraciones iniciales.

**Fase 2 - Agenda y citas:** Implementar modelo de cita, disponibilidad por doctor/consultorio, reglas de traslapes, horarios por día con intervalos de comida, configurar festivos y vacaciones, cancelación <24h y marcas, no-show, horizon de reserva (45d paciente, todo calendario staff).

**Fase 3 - Paciente y reservas:** Portal público, reserva con verificación de correo (código 6 dígitos), token de un solo uso, login del paciente, límites de reserva y bloqueo por 3 impuntualidades.

**Fase 4 - Pagos y reportes:** Modelo de pago manual, captura de saldo, métodos configurables (efectivo, tarjetas, SPEI), generación de reportes por paciente/consultorio, visualización de saldo real y abonos.

**Fase 5 - Privacidad y cumplimiento:** Aviso de privacidad final (jurídico), procedimiento ARCO implementado, datos sensibles revisados, datos clínicos fuera del MVP.

**Fase 6 - Notificaciones:** Proveedor WhatsApp/SMS investigado e integrado, plantillas de confirmación/alertas, envío por ambos canales según disponibilidad.

**Fase 7 - Sistema visual y rediseño integral:** Rediseñar todas las pantallas del MVP con experiencia mobile-first, temas claro/oscuro, accesibilidad, estados de carga/error/vacío, navegación por rol y revisión de codificación UTF-8. El dashboard de referencia está aprobado; la integración sigue pendiente. Ver `docs/UI_DESIGN_SYSTEM.md` y el plan por módulos en `docs/PLAN_UI_REDESIGN.md`.

**Fase 8 - Pruebas y lanzamiento:** Suite Playwright 75% cobertura, build limpio 0 errores/0 warnings, pruebas de runtime, documentación final, deploy y checklist de lanzamiento.

---

*Documento vivo: actualizarse en cada fase. Ver `Klinara.md` para el detalle técnico completo y `README.md` para visión general.*