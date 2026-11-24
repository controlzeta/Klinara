# SlotGeneratorService — Klinara

## Resumen

Background service (`IHostedService`) que ejecuta diariamente la generación automática de `TimeSlot` a partir de los `DoctorSchedule` vigentes. Garantiza que la agenda esté siempre actualizada para los próximos N días.

---

## Características

| Característica | Detalle |
|---|---|
| **Tipo** | `BackgroundService` (runs as `IHostedService`) |
| **Ejecución** | Diaria a hora fija UTC (default: 03:00) |
| **Ventana** | Días hacia adelante configurable (default: 30) |
| **Idempotente** | Sí — verifica existencia antes de insertar |
| **Exclusiones** | Festivos clínica, vacaciones doctor, lunch, festivos parciales |
| **Configuración** | Sección `SlotGenerator` en `appsettings.json` |

---

## Configuración

### appsettings.json (Producción)
```json
"SlotGenerator": {
  "Enabled": true,
  "DaysAhead": 30,
  "RunAtUtc": "03:00:00"
}
```

### appsettings.Development.json
```json
"SlotGenerator": {
  "Enabled": false,
  "DaysAhead": 7,
  "RunAtUtc": "03:00:00"
}
```

| Parámetro | Tipo | Default | Descripción |
|---|---|---|---|
| `Enabled` | bool | `true` | Habilita/deshabilita el job |
| `DaysAhead` | int | `30` | Días hacia adelante para generar slots |
| `RunAtUtc` | TimeSpan | `03:00:00` | Hora de ejecución diaria (UTC) |

---

## Lógica de Generación

```mermaid
flowchart TD
    A[Iniciar generación] --> B[Obtener clínicas activas con DoctorSchedules activos]
    B --> C[Agrupar por (ClinicId, DoctorId)]
    C --> D[Para cada doctor: obtener sus horarios]
    D --> E[Para cada horario: iterar días en rango efectivo]
    E --> F{¿Día coincide con DayOfWeek?}
    F -->|No| G[Siguiente día]
    F -->|Sí| H{¿Es festivo día completo?}
    H -->|Sí| G
    H -->|No| I{¿Doctor en vacaciones?}
    I -->|Sí| G
    I -->|No| J[Generar slots del día]
    J --> K[Excluir lunch]
    K --> L[Excluir festivos parciales]
    L --> M[Crear TimeSlot si no existe]
    M --> N[Siguiente slot]
    N --> O[Siguiente día]
    O --> P[Guardar cambios]
    P --> Q[Log resumen]
```

### Exclusiones Aplicadas

| Exclusión | Lógica |
|---|---|
| **Día de semana** | Solo genera para `DoctorSchedule.DayOfWeek` |
| **Festivo día completo** | `ClinicHoliday.IsFullDay = true` → salta día entero |
| **Festivo parcial** | `ClinicHoliday` con `StartTime/EndTime` → excluye slots en ese rango |
| **Vacaciones doctor** | `DoctorVacation` rango fechas → excluye días en rango |
| **Lunch** | `DoctorSchedule.LunchStart`–`LunchEnd` → excluye slots que se traslapen |
| **Duplicados** | Verifica `TimeSlot` existente por `(ClinicId, DoctorId, StartAt, EndAt)` |

---

## Registro en DI (Program.cs)

```csharp
builder.Services.AddSlotGenerator(builder.Configuration);
```

Esto registra:
- `SlotGeneratorOptions` desde configuración
- `SlotGeneratorService` como `IHostedService` (BackgroundService)

---

## API Pública

```csharp
public class SlotGeneratorService : BackgroundService
{
    // Generación manual (útil para tests o triggers externos)
    public async Task GenerateSlotsAsync(CancellationToken cancellationToken = default);
}
```

### Uso Manual
```csharp
// En un controller o comando
var slotGenerator = serviceProvider.GetRequiredService<SlotGeneratorService>();
await slotGenerator.GenerateSlotsAsync();
```

---

## Tests de Integración

Ejecutar test completo contra BD local:
```bash
cd /c/github/Klinara/SlotGeneratorTest && dotnet run
```

**Valida:**
- ✅ Genera slots para todos los días de la semana (Mon-Fri)
- ✅ Excluye lunch (13:00-14:00)
- ✅ Excluye festivo día completo
- ✅ Excluye vacaciones doctor
- ✅ Idempotente (2da ejecución = 0 slots nuevos)
- ✅ Procesa múltiples horarios por doctor

---

## Logs Típicos

```
[249dbec2] Iniciando generación de slots
[249dbec2] Completado - 5 horarios doctor, 120 slots creados
```

```
[fdcbb771] Iniciando generación de slots
[fdcbb771] Completado - 5 horarios doctor, 0 slots creados
```

---

## Próximas Mejoras

1. **Hangfire/Quartz** — Para mayor robustez (reintentos, dashboard, scheduling avanzado)
2. **Métricas** — Prometheus counters: `slots_generated_total`, `slots_skipped_holiday`, `slots_skipped_vacation`
3. **Notificaciones** — Alertar si generación falla o genera 0 slots inesperadamente
4. **API de trigger** — Endpoint POST `/api/slots/generate` para ejecución bajo demanda

---

## Referencias

- `docs/SCHEDULE_SERVICE.md` — Validación de horarios y slots
- `docs/APPOINTMENT_SERVICE.md` — Gestión de citas
- `docs/CHANGELOG.md` — Historial de cambios
- `docs/PLAN_PHASE1_EXECUTION.md` — Plan de ejecución