# Roles y Políticas de Autorización — Klinara

## Resumen de Roles

| Rol | Código | Alcance | Descripción |
|---|---|---|---|
| **Administrador de Plataforma** | `AdminPlataforma` | **Global** (todas las clínicas) | Acceso total a la plataforma: gestiona clínicas, usuarios globales, configuración de la plataforma, auditoría. |
| **Administrador de Clínica** | `AdminClinica` | **Por clínica** (su `ClinicId`) | Administra su clínica: usuarios, doctores, empleados, configuración de la clínica, reportes. |
| **Doctor** | `Doctor` | **Por clínica** (su `ClinicId`) | Gestiona su agenda, atiende pacientes, crea empleados, hereda permisos de Empleado. |
| **Empleado** | `Empleado` | **Por clínica** (su `ClinicId`) | Administra citas, pagos, desbloquea pacientes (tras 3 impuntualidades). |

> **Regla clave**: `AdminPlataforma` tiene `ClinicId = null`. Todos los demás roles **requieren** `ClinicId` no nulo.

---

## Políticas de Autorización (en `KlinaraPolicies.cs`)

### Políticas de Rol (Jerarquía)

| Política | Roles Permitidos | Uso |
|---|---|---|
| `PlatformAdminOnly` | `AdminPlataforma` | Endpoints globales: listar clínicas, métricas de plataforma, bootstrap admin. |
| `CanViewAllClinics` | `AdminPlataforma` | Igual que arriba, semánticamente explícita. |
| `ClinicAdminOrAbove` | `AdminClinica`, `AdminPlataforma` | Gestión de usuarios de clínica, configuración de clínica. |
| `DoctorOrAbove` | `Doctor`, `AdminClinica`, `AdminPlataforma` | Acceso a agenda médica, recetas, historiales. |
| `EmployeeOrAbove` | `Empleado`, `Doctor`, `AdminClinica`, `AdminPlataforma` | **Base mínima** para cualquier acción dentro de una clínica. |

### Políticas Funcionales (Permisos Granulares)

| Política | Roles Permitidos | Acción Protegida |
|---|---|---|
| `CanManageUsers` | `AdminClinica`, `AdminPlataforma` | CRUD usuarios de la clínica, asignar roles. |
| `CanManageAppointments` | `Empleado`, `Doctor`, `AdminClinica`, `AdminPlataforma` | Crear/editar/cancelar citas, horarios. |
| `CanManagePayments` | `Empleado`, `Doctor`, `AdminClinica`, `AdminPlataforma` | Registrar pagos, ver saldos, reportes financieros. |
| `CanUnblockPatients` | `Empleado`, `Doctor`, `AdminClinica`, `AdminPlataforma` | Desbloquear paciente tras 3 marcas de impuntualidad. |

### Política de Aislamiento por Clínica

| Política | Lógica |
|---|---|
| `SameClinicOnly` | **Custom requirement**: Permite si el usuario es `AdminPlataforma` **O** tiene claim `ClinicId` válido. Se usa en endpoints que operan sobre datos de una clínica específica (ej. `GET /api/clinics/{id}/appointments`). |

---

## Uso en Controladores / Componentes Blazor

```razor
@attribute [Authorize(Policy = KlinaraPolicies.CanManageAppointments)]
```

```csharp
[Authorize(Policy = KlinaraPolicies.ClinicAdminOrAbove)]
public async Task<IActionResult> ManageUsers(Guid clinicId) { ... }
```

```csharp
// En Program.cs / Startup:
builder.Services.AddAuthorization(options =>
{
    KlinaraPolicies.Configure(options);
});
```

---

## Reglas de Aislamiento (Tenant Isolation)

1. **Query-time filtering**: Todo acceso a datos de clínica **debe** filtrar por `ClinicId` del usuario autenticado (claim `ClinicId`).
2. **AdminPlataforma bypass**: Si el usuario tiene rol `AdminPlataforma`, **no se filtra** por `ClinicId`.
3. **Validación en tests**: Ver `TenantIsolationTests.cs` — 7 tests cubren aislamiento por `ClinicId`.

---

## Seed de Roles (en `RoleSeeder.cs`)

```csharp
public static async Task SeedAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    foreach (var role in KlinaraRoles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}
```

Se ejecuta al arrancar la app (`Program.cs`): `await RoleSeeder.SeedAsync(app.Services);`

---

## Matriz de Permisos (Resumen)

| Acción | Empleado | Doctor | AdminClinica | AdminPlataforma |
|---|---|---|---|---|
| Ver agenda propia | ✅ | ✅ | ✅ | ✅ |
| Ver agenda de otros (misma clínica) | ❌ | ✅ | ✅ | ✅ |
| Crear/editar citas | ✅ | ✅ | ✅ | ✅ |
| Cancelar cita (<24h → marca) | ✅ | ✅ | ✅ | ✅ |
| Registrar pago | ✅ | ✅ | ✅ | ✅ |
| Ver saldos/reportes | ✅ | ✅ | ✅ | ✅ |
| Desbloquear paciente (3 impuntualidades) | ✅ | ✅ | ✅ | ✅ |
| Gestionar usuarios (CRUD) | ❌ | ❌ | ✅ | ✅ |
| Configurar clínica (horarios, festivos) | ❌ | ❌ | ✅ | ✅ |
| Ver todas las clínicas | ❌ | ❌ | ❌ | ✅ |
| Gestionar admins de plataforma | ❌ | ❌ | ❌ | ✅ |

---

## Tests de Verificación

- **TenantIsolationTests** (7): Aislamiento por `ClinicId`, índices únicos, admin global.
- **RolePolicyTests** (12): Políticas registradas, roles correctos por política, jerarquía.

**Ejecutar**: `dotnet test Klinara.Tests/Klinara.Tests.csproj`

---

## Referencias en Código

| Archivo | Qué Contiene |
|---|---|
| `Authorization/KlinaraRoles.cs` | Constantes de roles + listas `All` / `ClinicScoped` |
| `Authorization/KlinaraPolicies.cs` | `Configure(AuthorizationOptions)` con 11 políticas |
| `Data/RoleSeeder.cs` | Seed de roles al arranque |
| `Program.cs` | `AddAuthorization` + `RoleSeeder.SeedAsync` |
| `Tests/RolePolicyTests.cs` | 12 tests de políticas |
| `Tests/TenantIsolationTests.cs` | 7 tests de aislamiento |

---

*Documento vivo — actualizar al agregar roles/políticas.*