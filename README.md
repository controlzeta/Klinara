# Klinara

Sistema SaaS para administrar consultorios médicos.

## Descripción general

Klinara es una plataforma para gestionar consultorios independientes, con módulos de:
- Autenticación y control de roles (Administrador, Doctor, Empleado)
- Agenda y citas (con reglas de cancelación y marcas de impuntualidad)
- Portal de reserva para pacientes
- Gestión de pagos manuales
- Aviso de privacidad (por consultorio)

## Tecnología

- Frontend: Blazor
- Backend: .NET 10 LTS
- Base de datos: SQL Server

## MVP

1. Autenticación con correo y código de 6 dígitos (1h vigencia).
2. Dashboard con datos ficticios.
3. Gestión de citas: reserva, cancelación (<24h → marca; 3 marcas bloquean reserva en portal).
4. Pagos manuales: registro de abonos y saldo pendiente.

## Público objetivo

Clínicas y doctores independientes que necesitan organizar citas, pacientes y pagos en un solo lugar.

## Estado

Documentación del PRD construida en sesión Hermes — ver `Klinara.md` para el detalle completo.

## Próximos pasos

Definir definiciones pendientes: calendario de servicios, proveedores de WhatsApp/email, catálogo de tarifas, navegadores para pruebas Playwright y aviso de privacidad final.
