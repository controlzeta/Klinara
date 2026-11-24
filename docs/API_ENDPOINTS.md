# API Endpoints — Klinara

## Resumen

WebAPI REST para gestión de agenda y citas médicas. Todos los endpoints requieren autenticación y autorización por policies.

---

## Autenticación y Autorización

| Header | Requerido |
|---|---|
| `Authorization: Bearer <token>` | Sí |

| Policy | Roles | Controladores |
|---|---|---|
| `CanManageSchedules` | AdminPlataforma, AdminClinica, Doctor | Schedules, SlotGenerator |
| `CanManageAppointments` | AdminPlataforma, AdminClinica, Doctor | Appointments, Schedules (slots) |
| `CanManagePatients` | AdminPlataforma, AdminClinica, Doctor | Patients, Appointments (by patient) |
| `CanManageHolidays` | AdminPlataforma, AdminClinica | ClinicHolidays |
| `CanManageVacations` | AdminPlataforma, AdminClinica, Doctor | DoctorVacations |
| `CanUnblockPatients` | AdminPlataforma, AdminClinica | Appointments (unblock) |

---

## Endpoints

### Schedules (`/api/schedules`)

| Método | Ruta | Policy | Descripción |
|---|---|---|---|
| GET | `/clinic/{clinicId}/doctor/{doctorId}` | CanManageSchedules | Horarios de un doctor |
| GET | `/clinic/{clinicId}/doctor/{doctorId}/slots` | CanManageAppointments | Slots en rango de fechas |
| POST | `/clinic/{clinicId}/doctor/{doctorId}/slots/generate` | CanManageSchedules | Generar slots manualmente |
| GET | `/clinic/{clinicId}/doctor/{doctorId}/availability` | CanManageAppointments | Verificar disponibilidad |
| POST | `/` | CanManageSchedules | Crear horario |
| PUT | `/{id}` | CanManageSchedules | Actualizar horario |
| DELETE | `/{id}` | CanManageSchedules | Eliminar horario |

**Query params para slots:**
- `startDate` (DateTime, requerido)
- `endDate` (DateTime, requerido, máx 90 días)

**Query params para availability:**
- `startAt` (DateTime, requerido)
- `endAt` (DateTime, requerido)
- `excludeAppointmentId` (Guid, opcional)

---

### Appointments (`/api/appointments`)

| Método | Ruta | Policy | Descripción |
|---|---|---|---|
| GET | `/clinic/{clinicId}` | CanManageAppointments | Citas por clínica |
| GET | `/doctor/{doctorId}` | CanManageAppointments | Citas por doctor |
| GET | `/patient/{patientId}` | CanManagePatients | Citas por paciente |
| GET | `/{id}` | CanManageAppointments | Cita por ID |
| POST | `/` | CanManageAppointments | Crear cita |
| PUT | `/{id}` | CanManageAppointments | Actualizar cita |
| POST | `/{id}/cancel` | CanManageAppointments | Cancelar cita |
| POST | `/{id}/noshow` | CanManageAppointments | Marcar no-show |
| POST | `/patients/{patientId}/unblock` | CanUnblockPatients | Desbloquear paciente |

**Query params para listados:**
- `startDate` (DateTime, opcional)
- `endDate` (DateTime, opcional)

---

### Patients (`/api/patients`)

| Método | Ruta | Policy | Descripción |
|---|---|---|---|
| GET | `/clinic/{clinicId}` | CanManagePatients | Pacientes por clínica |
| GET | `/{id}` | CanManagePatients | Paciente por ID |
| POST | `/` | CanManagePatients | Crear paciente |
| PUT | `/{id}` | CanManagePatients | Actualizar paciente |

---

### ClinicHolidays (`/api/clinicholidays`)

| Método | Ruta | Policy | Descripción |
|---|---|---|---|
| GET | `/clinic/{clinicId}` | CanManageHolidays | Festivos por clínica |
| POST | `/` | CanManageHolidays | Crear festivo |
| DELETE | `/{id}` | CanManageHolidays | Eliminar festivo |

**Query params:**
- `startDate` (DateTime, opcional)
- `endDate` (DateTime, opcional)

---

### DoctorVacations (`/api/doctorvacations`)

| Método | Ruta | Policy | Descripción |
|---|---|---|---|
| GET | `/clinic/{clinicId}/doctor/{doctorId}` | CanManageVacations | Vacaciones por doctor |
| POST | `/` | CanManageVacations | Crear vacaciones |
| DELETE | `/{id}` | CanManageVacations | Eliminar vacaciones |

**Query params:**
- `startDate` (DateTime, opcional)
- `endDate` (DateTime, opcional)

---

### SlotGenerator (`/api/slotgenerator`)

| Método | Ruta | Policy | Descripción |
|---|---|---|---|
| POST | `/generate` | CanManageSchedules | Trigger manual generación slots |

---

## DTOs

### DoctorScheduleDto
```json
{
  "clinicId": "guid",
  "doctorId": "guid",
  "dayOfWeek": 1,
  "startTime": "09:00:00",
  "endTime": "14:00:00",
  "lunchStart": "13:00:00",
  "lunchEnd": "14:00:00",
  "slotDurationMinutes": 30,
  "effectiveFrom": "2026-01-01T00:00:00Z",
  "effectiveTo": "2026-12-31T23:59:59Z"
}
```

### CreateAppointmentDto
```json
{
  "clinicId": "guid",
  "doctorId": "guid",
  "patientId": "guid",
  "startAt": "2026-12-01T10:00:00Z",
  "endAt": "2026-12-01T10:30:00Z",
  "type": 2,
  "notes": "Consulta general",
  "createdByUserId": "guid",
  "isPatientBooking": false
}
```

### CancelAppointmentDto
```json
{
  "cancelledByUserId": "guid",
  "reason": "No puedo asistir",
  "isPatientCancellation": true
}
```

### CreateClinicHolidayDto
```json
{
  "clinicId": "guid",
  "name": "Día de Muertos",
  "date": "2026-11-02T00:00:00Z",
  "isRecurring": false,
  "isFullDay": true,
  "startTime": null,
  "endTime": null
}
```

### CreateDoctorVacationDto
```json
{
  "clinicId": "guid",
  "doctorId": "guid",
  "startDate": "2026-12-20T00:00:00Z",
  "endDate": "2026-12-30T23:59:59Z",
  "reason": "Vacaciones anuales"
}
```

---

## Códigos de Respuesta

| Código | Significado |
|---|---|
| 200 | OK |
| 201 | Created |
| 400 | Bad Request (validación fallida) |
| 401 | Unauthorized |
| 403 | Forbidden (policy no cumple) |
| 404 | Not Found |
| 500 | Error interno |

**Error 400 body:**
```json
{
  "errors": ["Mensaje de error 1", "Mensaje de error 2"]
}
```

---

## Ejemplos de Uso

### Crear cita (staff)
```bash
curl -X POST https://api.klinara.com/api/appointments \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "clinicId": "guid",
    "doctorId": "guid",
    "patientId": "guid",
    "startAt": "2026-12-01T10:00:00Z",
    "endAt": "2026-12-01T10:30:00Z",
    "type": 2,
    "notes": "Control mensual",
    "createdByUserId": "guid",
    "isPatientBooking": false
  }'
```

### Obtener slots disponibles para paciente
```bash
curl -X GET "https://api.klinara.com/api/schedules/clinic/{clinicId}/doctor/{doctorId}/slots?startDate=2026-12-01&endDate=2026-12-07" \
  -H "Authorization: Bearer <token>"
```

### Cancelar cita (paciente)
```bash
curl -X POST "https://api.klinara.com/api/appointments/{id}/cancel" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "cancelledByUserId": "guid",
    "reason": "Imprevisto",
    "isPatientCancellation": true
  }'
```

### Generar slots manualmente
```bash
curl -X POST "https://api.klinara.com/api/slotgenerator/generate" \
  -H "Authorization: Bearer <token>"
```

---

## Referencias

- `docs/SCHEDULE_SERVICE.md` — Lógica de validación
- `docs/APPOINTMENT_SERVICE.md` — Lógica de citas
- `docs/SLOT_GENERATOR_SERVICE.md` — Job automático
- `docs/ROLES_POLICIES.md` — Matriz de permisos