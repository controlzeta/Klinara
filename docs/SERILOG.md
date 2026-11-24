# Serilog Configuration — Klinara

## Resumen

Serilog configurado con:
- **Sink Console** — desarrollo, output compacto con CorrelationId
- **Sink File** — producción, rolling diario, retención 30 días
- **CorrelationId Enricher** — trazabilidad request/response completa
- **Request Logging Middleware** — log automático de cada HTTP request con metadata

---

## Paquetes Instalados

| Paquete | Versión | Propósito |
|---|---|---|
| `Serilog.AspNetCore` | 9.0.0 | Integración ASP.NET Core |
| `Serilog.Sinks.Console` | 6.0.0 | Consola (desarrollo) |
| `Serilog.Sinks.File` | 6.0.0 | Archivos rolling (producción) |
| `Serilog.Enrichers.CorrelationId` | 3.0.1 | CorrelationId automático |

---

## Configuración (`Program.cs`)

### Logger Global (antes de `builder.Build()`)

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()           // Agrega CorrelationId a cada log
    .Enrich.WithProperty("Application", "Klinara")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext} - {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/klinara-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {SourceContext} - {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();
```

### Middleware de Request Logging (temprano en pipeline)

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault() ?? "unknown");
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    };
    options.GetLevel = (httpContext, elapsed, ex) =>
        ex != null ? LogEventLevel.Error : LogEventLevel.Information;
});
```

### Cierre Limpio

```csharp
try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
```

---

## Estructura de Logs

### Consola (Desarrollo)
```
[14:32:15 INF] 0HM5K8J2Q9R3K Klinara.Web.Components.Pages.Appointments - Cita creada: ID=123, Paciente=Juan Pérez
[14:32:15 INF] 0HM5K8J2Q9R3K Microsoft.AspNetCore.Hosting.Diagnostics - Request finished in 45.2ms 200
```

### Archivo (Producción) — `logs/klinara-2026-11-22.log`
```
2026-11-22 14:32:15.123 -06:00 [INF] 0HM5K8J2Q9R3K Klinara.Web.Components.Pages.Appointments - Cita creada: ID=123, Paciente=Juan Pérez
2026-11-22 14:32:15.168 -06:00 [INF] 0HM5K8J2Q9R3K Microsoft.AspNetCore.Hosting.Diagnostics - Request finished in 45.2ms 200
```

---

## CorrelationId

- **Fuente**: `HttpContext.TraceIdentifier` (generado por ASP.NET Core por request)
- **Enriquecimiento automático**: `Enrich.WithCorrelationId()` + middleware `UseSerilogRequestLogging`
- **Disponible en**: Todos los logs dentro del scope del request
- **Uso en código**:
  ```csharp
  Log.Information("Procesando pago para cita {AppointmentId}", appointmentId);
  // Sale: [14:32:15 INF] 0HM5K8J2Q9R3K MiServicio - Procesando pago para cita 456
  ```

---

## Filtrado de Ruido (Microsoft/ASP.NET Core)

```csharp
.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
```
- Evita logs verbosos de framework (static files, routing, etc.)
- Solo WARNING/ERROR de Microsoft aparecen

---

## Retención y Archivos

| Configuración | Valor |
|---|---|
| Directorio | `logs/` (relativo a working directory) |
| Patrón | `klinara-YYYY-MM-DD.log` |
| Rolling | Diario (`RollingInterval.Day`) |
| Retención | 30 archivos (`retainedFileCountLimit: 30`) |
| Encoding | UTF-8 (default) |

---

## Seguridad: No Loguear Secretos

**Regla obligatoria**: Nunca loguear:
- OTPs / códigos de verificación
- Passwords / tokens
- Connection strings
- Datos sensibles de pacientes (LFPPP)

**Ejemplo correcto**:
```csharp
// ✅ CORRECTO
Log.Information("OTP enviado a {Email}", email); // No incluye el código

// ❌ INCORRECTO
Log.Information("OTP {Code} enviado a {Email}", code, email); // FILTRA EL CÓDIGO
```

**Enriquecimiento seguro para Identity**:
```csharp
// En servicios de auth, usar LogContext para agregar info sin secretos
using (LogContext.PushProperty("UserId", userId))
using (LogContext.PushProperty("ClinicId", clinicId))
{
    Log.Information("Login exitoso");
}
```

---

## Tests de Verificación

Los tests existentes (`TenantIsolationTests`, `RolePolicyTests`) corren con Serilog configurado y validan que:
- No hay excepciones de logging
- CorrelationId fluye correctamente
- Logs no contienen datos sensibles (verificar manualmente en `logs/`)

---

## Troubleshooting

| Problema | Solución |
|---|---|
| Logs no aparecen en consola | Verificar `MinimumLevel.Information()` y que `UseSerilog()` esté antes de `Build()` |
| Archivo no se crea | Verificar permisos de escritura en `logs/` (crear directorio si no existe) |
| CorrelationId vacío | Asegurar `UseSerilogRequestLogging` está **antes** de `UseAntiforgery`/`MapRazorComponents` |
| Archivos no rotan | Verificar `RollingInterval.Day` y que la app tiene uptime > 24h |

---

## Referencias

- [Serilog.AspNetCore](https://github.com/serilog/serilog-aspnetcore)
- [Serilog.Enrichers.CorrelationId](https://github.com/serilog/serilog-enrichers-correlationid)
- [Output Templates](https://github.com/serilog/serilog/wiki/Formatting-Output#output-template-syntax)

---

*Configuración lista para producción. Revisar logs en `logs/` tras primer despliegue.*