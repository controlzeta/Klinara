# Klinara

## Administrador de Consultorios

**Klinara** es un sitio web para administrar uno o varios consultorios desde una plataforma centralizada.

## Objetivo

Simplificar la operación diaria de consultorios mediante la gestión de usuarios, pacientes, citas, pagos y comunicación con pacientes.

## Modelo del servicio

Klinara será un SaaS en el que **cada cuenta corresponde a un consultorio**. Cada consultorio tendrá una URL propia, que determina a qué consultorio pertenece el paciente y su operación. Los datos de cada cuenta deben quedar aislados del resto de los clientes, con validación de permisos en el servidor además de la URL.

## MVP

1. Autenticación.
2. Dashboard inicial en formato mockup con datos ficticios.
3. Gestión de citas, incluyendo reserva por el paciente y notificaciones por WhatsApp y correo cuando esté disponible.
4. Gestión de pagos manuales.

## Plataforma y tecnología

- Sitio web SaaS para consultorios independientes con aislamiento de datos por cliente.
- Interfaz: **Blazor**.
- Backend: **.NET 10 LTS**.
- Base de datos: **SQL Server**.

## Usuarios y autenticación

### Roles y permisos

- **Administrador de clínica:** uno por consultorio; administra la cuenta y los usuarios de su consultorio.
- **Administrador de plataforma:** puede ver y modificar todos los datos y administrar todos los consultorios.
- **Doctor:** solo accede a su consultorio; puede dar de alta empleados y realizar las funciones del Empleado.
- **Empleado:** puede administrar y consultar citas y pagos.
- Todas las acciones privilegiadas, incluidas las del Administrador de plataforma, deben quedar registradas en la auditoría.

### Requisitos de autenticación

- Inicio de sesión con correo electrónico y contraseña.
- Cada usuario de la plataforma tendrá un identificador interno estable; el correo será único para iniciar sesión, no la clave primaria.
- Recuperación de contraseña por correo electrónico.
- Verificación del correo mediante un código numérico de 6 dígitos, válido por 1 hora.
- Mantener una tabla unificada de credenciales/tokens con el tipo, principal relacionado, expiración, uso y revocación. Debe distinguir código de verificación, token de paciente de un solo uso y sesión.
- La sesión de login tendrá una duración de 1 hora y será revocable por el usuario o al expirar.
- Contraseñas almacenadas con hashing adaptativo y salt; nunca guardar contraseñas en texto plano.
- Aplicar control de acceso por consultorio y rol.
- **Pendiente de diseño:** definir cómo se relaciona el token del paciente con esa tabla, dado que el paciente no crea una cuenta con contraseña.

### Datos del consultorio

- Nombre del consultorio.
- Dirección.
- Teléfono.
- Correo electrónico.

## Auditoría y seguridad

- Mantener historial de cambios para los registros del sistema, incluyendo usuario, fecha/hora, acción y campos modificados; registrar valores anteriores y nuevos cuando corresponda.
- Mantener campos de creación/modificación en los registros: usuario creador/modificador y fechas/horas.
- Auditar las acciones del Administrador de plataforma y definir si también se registra el acceso de lectura a datos de pacientes.

## Borrador de aviso de privacidad para pacientes (México)

- Klinara generará un aviso por consultorio usando el nombre, domicilio y datos de contacto de su perfil, junto con finalidades básicas y el medio para ejercer derechos ARCO. El Administrador del consultorio deberá revisar y completar los datos antes de publicarlo.

> **Borrador de producto, no aviso legal final.** Debe completarse y revisarse por asesoría jurídica antes del lanzamiento. La legislación mexicana requiere informar, entre otros puntos, la identidad y domicilio del responsable, los datos tratados, las finalidades, las opciones para limitar su uso y los mecanismos para ejercer derechos ARCO.[1]

**Responsable:** [nombre o razón social del consultorio], con domicilio en [domicilio completo] y contacto de privacidad en [correo/teléfono]. La distribución de responsabilidades entre el consultorio y Klinara como proveedor SaaS debe validarse contractualmente y con asesoría legal.

**Datos tratados en el MVP:** datos de identificación y contacto (nombre, apellidos, fecha de nacimiento, celular y, cuando se proporcione, teléfono de casa y correo); datos de agenda, reservas, cancelaciones e impuntualidad; y datos de pagos manuales (monto, fecha, método, concepto y saldo pendiente capturado). El expediente, diagnóstico y notas clínicas quedan fuera del MVP.

**Finalidades:** administrar pacientes y citas; verificar el correo para reservas; enviar confirmaciones y alertas por WhatsApp y, cuando se proporcione, por correo; registrar abonos y saldos; mantener la seguridad, auditoría y soporte del servicio. Las campañas promocionales y cuestionarios quedan fuera del MVP y requerirán definir un consentimiento separado.

**Proveedores y comunicación:** para operar el servicio podrían tratar datos los proveedores de alojamiento, correo y WhatsApp. Antes del lanzamiento se deben identificar los proveedores, sus ubicaciones, las transferencias aplicables y las condiciones de tratamiento.

**Derechos y contacto:** las personas podrán solicitar acceso, rectificación, cancelación u oposición (ARCO), así como consultar los medios para limitar el uso o divulgación de sus datos, mediante [procedimiento, correo y domicilio por definir]. También se debe definir cómo se comunicará cualquier cambio a este aviso.

**Datos sensibles y financieros:** el MVP no almacenará expedientes clínicos. La ley considera sensibles ciertos datos que revelan el estado de salud y establece requisitos de consentimiento para datos sensibles y financieros; el tratamiento de los datos de citas y pagos deberá revisarse jurídicamente antes de producción.[1] Si Klinara agrega datos clínicos después del MVP, será necesario revisar el aviso, las finalidades y los controles antes de incorporarlos.

**Conservación y seguridad:** definir plazos de conservación, eliminación/bloqueo y tratamiento ante solicitudes de baja. Klinara debe aplicar aislamiento por consultorio, accesos según rol, historial de cambios y medidas técnicas y organizativas de seguridad.

**Pendiente para publicar:** completar la identidad/domicilio del responsable, contacto de privacidad, proveedores y transferencias, plazos de conservación y procedimiento operativo ARCO. Este texto es un borrador y no sustituye la revisión legal.

## Dashboard inicial

- En el MVP se construirá un mockup con datos ficticios (fake data), para conectar datos reales después del MVP.
- El mockup mostrará el resumen de citas, el paciente de la cita actual y de las siguientes citas, y campañas de marketing.
- Las campañas de marketing reales quedan fuera del MVP; se revisarán para una etapa posterior.

## Requisitos no funcionales

- Configurar la zona horaria de cada consultorio y presentar las citas en esa zona.
- Diseñar la experiencia con enfoque **mobile-first**.
- Aislar los datos por consultorio y aplicar permisos por rol.
- Mantener historial de auditoría de cambios y acciones privilegiadas.

## Criterios de aceptación y pruebas

- Definir criterios verificables para inicio de sesión, alta de paciente, reserva/cancelación de citas y registro/corrección de abonos.
- Crear una suite automatizada de pruebas **Playwright** con una meta de cobertura de al menos **75%**.
- Precisar qué significa el 75% (cobertura de flujos/criterios de aceptación o cobertura de código) y cómo se medirá; los flujos críticos deben pasar en CI antes de liberar.

## Módulo de citas y pacientes

- El MVP manejará datos de identificación/contacto y la información necesaria para citas y pagos, sin expediente clínico, diagnósticos ni notas médicas.
- La información clínica se considera alcance posterior al MVP y requerirá revisión de privacidad, permisos y consentimiento antes de incorporarse.

### Alta de paciente

**Datos requeridos**

- Nombre.
- Apellido paterno.
- Fecha de nacimiento.
- Celular.

**Datos opcionales para alta por el personal**

- Apellido materno.
- Teléfono de casa.
- Correo electrónico, salvo que el paciente vaya a usar el portal de reservas (en ese caso es obligatorio).
- El correo del paciente no puede repetirse dentro del mismo consultorio; el mismo correo puede registrarse en consultorios distintos.

### Agenda de citas

- El personal y los pacientes podrán agendar citas dentro del MVP.
- El consultorio define la duración de las citas.
- Configurar disponibilidad por doctor y por consultorio; permitir horarios por día e intervalos no disponibles para comida/descanso.
- No permitir traslapes de citas.
- Para el paciente, reservar requiere al menos 24 horas de anticipación y solo podrá ver fechas hasta 45 días hacia adelante. El personal del consultorio puede ver todo el calendario y agendar más allá de ese límite.
- Configurar días festivos y vacaciones para bloquear disponibilidad.
- Cancelación tardía permitida: cada cancelación con menos de 24 horas de anticipación agrega una marca de impuntualidad. A la tercera marca se bloquean las reservas desde el portal; el Empleado aún puede agendar en nombre del paciente. El bloqueo es permanente hasta que cualquier rol del consultorio (Administrador de clínica, Doctor o Empleado) lo desbloquee.
- Registrar una marca de **no-show** independiente en el historial cuando el paciente no asista; no cuenta para las 3 marcas de impuntualidad que bloquean reservas por portal.
- Enviar confirmaciones y alertas por WhatsApp; enviar también por correo electrónico cuando el paciente tenga correo registrado.
- Confirmaciones y alertas están dentro del MVP; investigar y seleccionar proveedor de WhatsApp/correo antes de cerrar diseño e implementación.

### Portal del paciente

- El paciente podrá reservar desde el portal en el MVP y acceder mediante un enlace/token de un solo uso, sin crear contraseña.
- Para reservar, confirmar el correo electrónico mediante un código numérico de 6 dígitos.
- Definir por separado la expiración, revocación y alcance del código de verificación, del enlace/token de acceso y de la sesión del usuario.
- Investigar proveedores y costos de WhatsApp y correo; no asumir que existe un servicio gratuito apto para producción.

## Módulo de pagos

- Registro y consulta de pagos manuales; cada pago se asocia al paciente.
- Registrar por pago: monto, fecha, método de pago y concepto.
- Permitir varios abonos.
- Registrar manualmente el saldo pendiente registrado.
- Mostrar además el **saldo real**, calculado como total adeudado menos los abonos registrados. Definir cómo se registra el total adeudado (por ejemplo, importe de consulta/servicio u otro concepto).
- Los métodos de pago se configuran por consultorio; valores iniciales: efectivo, tarjetas y SPEI.
- Doctor y Empleado pueden registrar y corregir pagos.
- Generar reportes por paciente y por consultorio en el MVP.
- **Post-MVP:** emitir y enviar recibos por pago por correo y/o WhatsApp; permitir que el paciente consulte su saldo pendiente y sus abonos.

## Módulo de campañas (post-MVP)

- Envío de campañas promocionales por correo electrónico.
- Envío periódico de mensajes por WhatsApp con recomendaciones o información de precios nuevos, sujeto al consentimiento del paciente.
- Creación y envío de cuestionarios para pacientes.
- Registrar y respetar las preferencias de consentimiento para comunicaciones promocionales.
- Estas funcionalidades se contemplan para una etapa posterior al MVP.

## Definiciones pendientes para cerrar el PRD del MVP

### Producto y tenancy

- Definir el formato de la URL propia del consultorio (subdominio o ruta) y cómo se crea/activa una cuenta.
- Precisar el flujo de alta de doctores y empleados.

### Agenda y reservas

- Definir si la regla contra citas simultáneas aplica por doctor, por consultorio o en ambos niveles.
- Definir si horarios, festivos y vacaciones se configuran por consultorio, por doctor o en ambos; y qué pasa con citas ya reservadas cuando se agrega un bloqueo.
- Definir qué alertas se envían y con cuánta anticipación.
- Investigar exhaustivamente y elegir proveedor de WhatsApp y correo; definir cómo confirmar/corregir el número de WhatsApp y manejar fallas de entrega.

- Definir reglas para reprogramar citas y quién puede modificar una cita existente.

### Autenticación, pagos y privacidad

- Definir vencimiento y alcance del token de paciente de un solo uso, cómo se relaciona con la tabla de tokens y cómo se distingue del código OTP y de la sesión.
- Definir límites de intentos/reenvíos de OTP y recuperación de contraseña.
- Definir cómo se captura el total adeudado para calcular el saldo real y qué datos se incluyen en el reporte.
- Completar datos del responsable del aviso de privacidad, proveedores/transferencias, conservación y procedimiento ARCO; confirmar el flujo para revisar/publicar el aviso generado por consultorio.

### Calidad y operación técnica

- Decidir si el 75% corresponde a cobertura de ramas/decisiones, cobertura de flujos E2E o a ambas, y cómo se mide en CI.
- La suite Playwright debe cubrir como mínimo login, alta de paciente, reserva de cita, registro de abono y confirmación de cita; definir navegadores soportados.
- Definir respaldos/recuperación, disponibilidad y objetivos de rendimiento.
- Acordar métricas de éxito del MVP.





## Decisión de versión de .NET

Se decidió iniciar el proyecto con **.NET 10 LTS**, en lugar de .NET 9, para utilizar una versión con soporte activo.

## Sources

[1] https://www.diputados.gob.mx/LeyesBiblio/pdf/LFPDPPP.pdf — Ley Federal de Protección de Datos Personales en Posesión de los Particulares, texto vigente
