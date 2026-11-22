# Klinara — Product Requirements Document

## Phase 1: Fundation & MVP Scope (Nov 2026)

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
   - Reserva paciente: ver correo con código 6 dígitos; token de un solo uso; expiración definida.
   - Horizon de reserva: paciente ve hasta 45 días; personal ve todo calendario.
4. **Gestión de pagos manuales:** 
   - Registrar pago: monto, fecha, método (efectivo, tarjetas, SPEI), concepto.
   - Saldo pendiente: captura manual; mostrar saldo real = total adeudado - abonos registrados.
   - Reportes por paciente y consultorio.
5. **Aviso de privacidad (borrador):** Generado por Klinara por consultorio usando perfil; admin debe revisar y completar antes de publicar. Include identidad, domicilio, finalidades básicas y medio ARCO.

**POST-MVP (Fase 2 en adelante):**

- Campañas promocionales por email/WhatsApp con consentimiento.
- Cuestionarios para pacientes.
- Recibos automáticos por pago.
- Paciente consulte saldo y abonos.
- Datos clínicos/expte médico.
- Integración con proveedores de WhatsApp/SMS definidos.
- Catálogo de servicios y tarifas configurables.

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

### 8. Pendientes para Fase 2

- Catálogo de servicios y tarifas por consultorio.
- Proveedor de WhatsApp/SMS definido y testeado.
- Integración de datos clínicos (post-MVP).
- Aviso de privacidad final (jurídicamente revisado).
- Métricas de éxito del MVP (consultorios activos, reservas completadas, reducción no-show).

---

*Documento vivo: actualizarse en cada fase. Ver `Klinara.md` para el detalle técnico completo y `README.md` para visión general.*