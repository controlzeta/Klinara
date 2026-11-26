# Klinara

Sistema SaaS multi-tenant para la administración y operación de consultorios médicos independientes y clínicas.

---

## 📋 Descripción General

Klinara resuelve la operación diaria de consultorios médicos mediante un modelo multi-inquilino (*multi-tenant*) con aislamiento por clínica (`ClinicId` y subdominio). Ofrece tanto un panel administrativo para el personal médico como un portal público de autoservicio para pacientes con autenticación sin contraseña (*passwordless* basada en tokens y OTP).

---

## 🚀 Estado del MVP (v1.0)

El proyecto cuenta con las fases centrales del MVP implementadas y validadas con **86 pruebas unitarias e integración superadas**:

- **Fase 1 (Infraestructura y Auth):** ✅ .NET 10 LTS, Blazor Interactive Server, EF Core + SQL Server, Serilog con CorrelationId, autenticación con OTP de 6 dígitos, roles (`PlatformAdmin`, `ClinicAdmin`, `Doctor`, `Employee`) y aislamiento por clínica.
- **Fase 2 (Agenda y Citas):** ✅ Reglas de disponibilidad, horarios por doctor, descansos de comida, festivos y vacaciones, validación de traslapes, detección de cancelaciones tardías (<24h), registro independiente de No-Show, regla de 3 strikes de impuntualidad y desbloqueo por personal autorizado.
- **Fase 3 (Portal del Paciente):** ✅ Portal web público y adaptativo (`/portal/{subdominio}`), verificación de correo por OTP (6 dígitos, 1h), emisión de tokens seguros (`PortalAccess`, `AppointmentConfirmation`, `AppointmentManagement`), reserva de citas con horizonte controlado (24h min / 45d máx), confirmación, consulta en modo sólo lectura y cancelación protegida.
- **Fase 4 (Pagos Manuales y Saldos):** ✅ Registro de cobros manuales (`Efectivo`, `Tarjeta de Crédito/Débito`, `SPEI`, `Otro`), vinculación a paciente/cita, captura de adeudo y cálculo dinámico de saldo pendiente (`TotalDue - TotalPaid`).
- **Dashboard Operativo:** ✅ Métricas en tiempo real en la pantalla de inicio del staff (pacientes activos, citas del día, total recaudado y saldo por cobrar).

---

## 🛠️ Stack Tecnológico

| Capa | Tecnología |
|---|---|
| **Framework Backend** | .NET 10 LTS (C# 13) |
| **Frontend / UI** | Blazor Web App (Interactive Server Mode) + Bootstrap 5 |
| **Persistencia & ORM** | Entity Framework Core con Microsoft SQL Server |
| **Identidad & Seguridad** | ASP.NET Core Identity + Tokens criptográficos de un solo uso |
| **Logging & Diagnóstico** | Serilog (Consola y Archivo con Correlation ID y enriquecimiento HTTP) |
| **Testing** | xUnit + Entity Framework Core InMemory (86 tests passing) |

---

## 🧭 Flujos Principales Implementados

### 1. Flujo Administrativo del Staff (`/staff/*`)
- **Login Staff:** Ingreso seguro con correo, contraseña y verificación OTP.
- **Directorio de Pacientes (`/staff/patients`):** Alta inmediata, consulta de saldo, historial de impuntualidades y desbloqueo de pacientes sancionados.
- **Agenda de Citas (`/staff/appointments`):** Programación directa de citas sin límite de horizonte, cancelación administrativa y marcado de *No-Show*.
- **Control Financiero (`/staff/payments`):** Registro de pagos y abonos en efectivo/tarjeta/SPEI, visualización de saldo real y desglose de movimientos.
- **Dashboard (`/`):** Indicadores operativos y accesos directos al portal de cada consultorio.

### 2. Flujo del Portal del Paciente (`/portal/{subdominio}`)
1. **Inicio:** `GET /portal/{subdominio}` — Vista pública de la clínica.
2. **Verificación:** Ingreso de correo y validación de código OTP de 6 dígitos.
3. **Reserva:** Selección de especialista, fecha y ranura disponible (respeta descansos y festivos).
4. **Confirmación:** Correo con token de confirmación (`GET /portal/{subdominio}/confirm?token=...`).
5. **Gestión:** Consulta de cita (`GET /portal/{subdominio}/appointment/{token}`) y cancelación con validación de política tardía.

---

## 📂 Estructura del Repositorio

```text
Klinara/
├── Klinara.Web/                   # Proyecto principal Blazor Web App (.NET 10)
│   ├── Components/
│   │   ├── Account/               # Vistas de autenticación Identity y 2FA
│   │   ├── Layout/                # MainLayout, NavMenu interactivo
│   │   ├── Pages/                 # Home (Dashboard), StaffPatients, StaffAppointments, StaffPayments
│   │   └── PatientPortal/         # Vistas públicas del paciente (Verify, Book, Confirm, View, Cancel)
│   ├── Controllers/               # Controladores API REST (Appointments, Patients, Payments, PatientPortal)
│   ├── Data/                      # Modelos EF Core (Clinic, Appointment, Patient, Payment, TimeSlot, etc.)
│   └── Services/                  # Lógica de negocio (AppointmentService, ScheduleService, PaymentService, PatientTokenService)
├── Klinara.Tests/                 # Suite de pruebas automatizadas (86 tests)
└── docs/                          # Documentación del proyecto
    ├── PRD.md                     # Product Requirements Document
    ├── PRD_COMPLIANCE.md          # Checklist de cumplimiento vs PRD
    ├── PLAN_FASE3_PATIENT_PORTAL.md # Especificación y checklist de despliegue del portal
    └── UI_DESIGN_SYSTEM.md        # Guía de estilos y lineamientos UI
```

---

## ⚡ Guía de Inicio Rápido

### Prerrequisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- SQL Server (LocalDB, Express o contenedor Docker)

### Ejecutar la Aplicación

1. **Clonar el repositorio:**
   ```bash
   git clone https://github.com/tu-usuario/Klinara.git
   cd Klinara
   ```

2. **Configurar la cadena de conexión:**
   Revisar `Klinara.Web/appsettings.json` o establecer la variable de entorno:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=KlinaraDb;Trusted_Connection=True;MultipleActiveResultSets=true"
   }
   ```

3. **Ejecutar migraciones de base de datos:**
   ```bash
   dotnet ef database update --project Klinara.Web
   ```

4. **Iniciar el servidor de desarrollo:**
   ```bash
   dotnet run --project Klinara.Web
   ```
   La aplicación estará accesible en `https://localhost:5001` (o puerto configurado).

### Ejecutar las Pruebas Automatizadas

```bash
dotnet test Klinara.Tests/Klinara.Tests.csproj
```

---

## 📌 Configuración para Producción (Checklist)

Variables de entorno requeridas en despliegue:

- `Smtp:Host`: Servidor SMTP (ej. SendGrid, Mailgun).
- `Smtp:Port`: Puerto SMTP (587 recomendado).
- `Smtp:Username` y `Smtp:Password`: Credenciales del servicio de correo.
- `Smtp:FromEmail` y `Smtp:FromName`: Remitente oficial (`citas@klinara.com`).
- `PatientPortal:BaseUrl`: URL base del portal (`https://{0}.klinara.app/portal`).

---

## 🗺️ Próximos Pasos (Roadmap Post-MVP)

- [ ] Integración oficial de proveedor de WhatsApp (Twilio / Gupshup) para confirmaciones y alertas.
- [ ] Redacción jurídica final del Aviso de Privacidad y procedimientos ARCO (LFPPP México).
- [ ] Suite de pruebas de extremo a extremo (E2E) con Playwright.
