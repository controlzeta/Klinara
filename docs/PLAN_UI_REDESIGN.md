# Plan de la Fase 7 — Rediseño de todas las pantallas

**Estado:** planeada; implementación pendiente.  
**Alcance:** interfaz web de Klinara, componentes compartidos y flujos visuales del MVP.  
**Referencia visual:** dashboard azul, limpio y enfocado en salud, aprobado como dirección visual.

## Objetivo

Aplicar un sistema visual coherente a todas las pantallas del MVP, manteniendo las reglas de negocio, permisos y validaciones existentes. Diseñar primero para móvil y adaptar después a escritorio. La interfaz debe indicar claramente cuándo se muestran datos de demostración.

## Estado inicial verificado

- `Klinara.Web/Components/Pages/Home.razor` todavía contiene la pantalla de plantilla (`Hello, world!`).
- `Klinara.Web/Components/Layout/NavMenu.razor` conserva navegación de plantilla en inglés; no representa aún la navegación funcional de Klinara.
- `wwwroot/klinara-theme.css` y `wwwroot/klinara-theme.js` existen como archivos locales sin seguimiento de Git y todavía no están conectados desde `Components/App.razor`.
- Por lo tanto, el rediseño visual aún no está integrado en la aplicación. Estos archivos deben revisarse y probarse antes de decidir si se reutilizan.

## Inventario de pantallas

| Área | Pantallas y estados a cubrir |
|---|---|
| Autenticación | Inicio de sesión, captura/verificación OTP, recuperación de contraseña, expiración de sesión, errores y confirmación de correo. |
| Inicio | Dashboard con agenda del día, próxima cita, indicadores y pagos recientes. El dashboard puede usar datos ficticios mientras siga marcado como demostración. |
| Pacientes | Lista/búsqueda, alta, edición y ficha/resumen; estados vacío, cargando, validación y error. |
| Agenda | Vistas de día/semana, selección de horario, crear/editar cita, confirmar, cancelar, no-show y conflictos de disponibilidad. |
| Portal del paciente | Verificación por correo, disponibilidad, reserva, confirmación y consulta/cancelación de citas según las reglas del MVP. |
| Pagos | Lista, registro de pago/abono, método, concepto, fecha, saldo capturado y reportes por paciente/consultorio. |
| Configuración de consultorio | Datos del consultorio, usuarios/roles, horarios por día, comida, disponibilidad por doctor, festivos y vacaciones. |
| Administración de plataforma | Selección/gestión de consultorios y estados de acceso, respetando el alcance global del rol. |
| Transversales | Sin acceso, no encontrado, errores, confirmaciones, carga, listas vacías y avisos de validación. |
| Post-MVP | Campañas y cuestionarios: documentar su futura integración, sin presentarlos como funcionalidad disponible en MVP. |

## Orden de trabajo

### 7.0 — Auditoría y fundamentos
- Inventariar rutas, componentes, permisos y flujos existentes antes de modificar vistas.
- Acordar el mapa de navegación por rol; no mostrar enlaces activos a módulos que aún no existan.
- Aplicar `docs/UI_DESIGN_SYSTEM.md` como fuente de colores, tipografía, espaciado y componentes.
- Confirmar estructura compartida, responsive design, tema claro/oscuro y estados de carga/error/vacío.
- Registrar capturas iniciales para comparar el resultado sin reemplazar pruebas funcionales.

### 7.1 — Shell, navegación y autenticación
- Rediseñar sidebar, selector de consultorio, encabezado, navegación móvil y perfil/sesión.
- Aplicar el mismo sistema a login, OTP y recuperación de contraseña.
- Mostrar errores de autenticación y expiración con mensajes claros, sin filtrar detalles técnicos.
- Restringir u ocultar acciones según roles y mantener los controles de autorización del servidor.

### 7.2 — Dashboard
- Implementar el dashboard aprobado: indicadores, agenda diaria, próxima cita, acciones rápidas, actividad y pagos recientes.
- Mantener una etiqueta visible de demostración mientras sus datos sean ficticios.
- No usar nombres ni cifras ficticias fuera de las vistas de demo.

### 7.3 — Pacientes
- Diseñar búsqueda, filtros, lista, formularios y resumen del paciente.
- En móvil, convertir tablas anchas en tarjetas o filas adaptables sin perder acciones ni datos.
- Cubrir validación, duplicados por consultorio, estados sin resultados y errores de guardado.

### 7.4 — Agenda y citas
- Diseñar vistas de agenda, selector de fecha/hora y formularios de creación/edición.
- Representar disponibilidad, comida, vacaciones, festivos y conflictos sin depender solo del color.
- Explicar las reglas de cancelación de 24 horas, anticipo mínimo de 24 horas y máximo de 45 días para el portal.
- Diferenciar cita confirmada, pendiente, atendida, cancelada y no-show.

### 7.5 — Portal del paciente
- Crear una experiencia móvil sencilla para ver disponibilidad, verificar correo con código y reservar.
- Comunicar límites de reserva, resultado de confirmación, token inválido/expirado y cita cancelada.
- Mantener aislados los datos por consultorio y mostrar el aviso de privacidad correspondiente.

### 7.6 — Pagos y reportes
- Diseñar captura de pagos y abonos, detalle por paciente, métodos configurados y reportes.
- Separar claramente saldo registrado manualmente, total adeudado y pagos acumulados.
- Usar formatos de moneda/fecha de México y evitar presentar cálculos que el MVP no realice.

### 7.7 — Configuración y administración
- Diseñar horarios, disponibilidad, comida, vacaciones, festivos, usuarios y métodos de pago.
- Diseñar la vista de administración de plataforma separada de la administración del consultorio.
- Mostrar el contexto del consultorio activo y confirmar acciones que afecten datos o permisos.

### 7.8 — Accesibilidad, codificación y consistencia
- Revisar teclado, foco visible, etiquetas de formulario, mensajes asociados a campos y orden semántico.
- Buscar contraste WCAG AA como objetivo; los estados deben tener texto/icono además de color.
- Guardar fuentes de texto en UTF-8; mantener `<meta charset="utf-8">` y establecer `lang="es"`.
- Verificar en navegador textos con acentos/ñ, por ejemplo: “Clínica Santa Lucía”, “Próxima cita”, “María Rodríguez” y “Configuración”.
- Añadir una comprobación para detectar el carácter de reemplazo `�` en textos visibles y archivos fuente.

### 7.9 — Validación y salida
- Probar en viewport móvil de 320–390 px, tablet de 768 px y escritorio de 1280–1440 px.
- Revisar modo claro y oscuro, navegación por teclado, responsive y estados vacíos/de error.
- Ejecutar build Release y pruebas de componentes/rutas; agregar pruebas Playwright para los flujos críticos del MVP.
- Confirmar que ninguna pantalla enlaza a una ruta inexistente y que las pantallas sin implementar se identifican como pendientes.

## Criterios de aceptación de la fase

- Todas las pantallas del inventario tienen una ruta/componente responsable, un rol autorizado y estados de carga, vacío, error y éxito documentados.
- No queda texto ni navegación residual de la plantilla.
- Los módulos no implementados no parecen funcionales ni llevan a rutas rotas.
- El dashboard conserva una etiqueta de datos de demostración hasta su conexión a datos reales.
- El diseño responde a móvil y escritorio y mantiene equivalencia funcional entre tema claro y oscuro.
- Los textos españoles se muestran correctamente, incluidos acentos y `ñ`; no aparecen secuencias mojibake ni `�`.
- Los flujos existentes y sus permisos siguen funcionando; las pruebas críticas acordadas pasan en CI.
- Build Release sin errores y capturas de validación guardadas como evidencia de la fase.

## Dependencias y riesgos

- Alinear rutas y navegación con `docs/ROLES_POLICIES.md` y las reglas del PRD antes de activar enlaces.
- Conectar datos reales solo cuando los servicios/backend estén listos; no convertir datos de demo en aparentes datos reales.
- Validar con especial atención los límites de aislamiento por consultorio en selector, lista, detalle y reportes.
- Revisar la codificación real en el archivo que origina el texto; no intentar corregir mojibake sustituyendo caracteres de forma global sin identificar la causa.
