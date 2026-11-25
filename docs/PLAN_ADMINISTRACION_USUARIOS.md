# Plan — Administración de usuarios y roles

## Objetivo
Habilitar a administradores autorizados para consultar usuarios y asignarles un rol de Klinara, de forma segura y útil para pruebas de permisos.

## Estado actual verificado
- Hay cuatro roles y una política `CanManageUsers` para AdminClinica/AdminPlataforma.
- No existe página ni servicio de administración de usuarios.
- `RoleSeeder` crea roles, pero no hay cuenta inicial de plataforma; agregar solo UI no bastaría si nadie tiene `AdminPlataforma`.
- El repositorio tiene cambios locales de documentación y archivos de Visual Studio; preservarlos.

## Decisiones y límites
- Crear administración en `/admin/users`; la ruta Blazor y el servicio validan permisos en servidor.
- AdminClinica ve y administra únicamente usuarios de su `ClinicId`; no puede crear/otorgar `AdminPlataforma`.
- AdminPlataforma puede ver usuarios de todas las clínicas y cambiar roles.
- Un cambio reemplaza el conjunto de roles por uno solo de los cuatro roles oficiales.
- No permitir que una persona se quite su propio rol, ni quitar el último AdminPlataforma.
- Para asignar un rol de clínica a un usuario sin `ClinicId`, solo AdminPlataforma puede elegir la clínica; al asignar AdminPlataforma, el `ClinicId` queda nulo.
- Proveer bootstrap opcional y explícito mediante `Klinara:BootstrapPlatformAdminEmail` (user-secrets/env var), sin promover usuarios automáticamente por ser el primero registrado.
- El cambio invalida security stamp; la persona afectada debe volver a iniciar sesión para recargar sus claims.

## Fases
1. Pruebas RED para autorización, scope de clínica, cambios de rol, protección del último admin y bootstrap.
2. Servicio `IUserAdministrationService` que aplique las reglas sin confiar en IDs de clínica enviados por usuarios no globales.
3. Página Blazor interactiva para listar usuarios, mostrar clínica/roles y cambiar rol; selector de clínica solo para AdminPlataforma.
4. Enlace de navegación condicionado a `CanManageUsers` y bootstrap explícito de cuenta administrativa.
5. Ejecutar suite completa y rebuild aislado; documentar cómo activar bootstrap con user-secrets.

## Aceptación
- AdminClinica no puede listar ni editar usuarios de otra clínica ni concederse AdminPlataforma.
- AdminPlataforma puede listar usuarios globalmente y asignar roles/clínicas válidos.
- Un cambio persiste un único rol, actualiza `ClinicId` conforme a las reglas e invalida sesiones previas vía security stamp.
- No se puede auto-remover ni remover el último AdminPlataforma.
- La página no aparece en navegación para usuarios sin permiso y la URL directa/API del servicio tampoco permite operar sin autorización.
- Tests focalizados + suite completa pasan; build con cero errores, advertencias existentes reportadas.

## Archivos probables
- `Klinara.Web/Services/UserAdministrationService.cs`
- `Klinara.Web/Components/Pages/AdminUsers.razor`
- `Klinara.Web/Components/Layout/NavMenu.razor`
- `Klinara.Web/Data/RoleSeeder.cs`, `Program.cs`
- `Klinara.Tests/UserAdministrationServiceTests.cs`
- Este plan
