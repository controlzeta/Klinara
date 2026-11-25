---
name: klinara-patientportal
description: "Use when implementing or reviewing Klinara patient portal."
version: 1.1.0
author: Hermes Agent
category: software-development
---

# Klinara Patient Portal

## Workflow

1. For a repository-status or plan review, establish the current branch, ahead/behind state, worktree changes, and recent commits before assessing feature progress. Separate committed work from uncommitted IDE artifacts and untracked files.
2. Read the portal plan's current-state table, sprint breakdown, tests, and Definition of Done. Treat plan checkboxes and prose as requirements, not proof of implementation; compare every claimed completion against current source files, routes, TODOs, and test projects.
3. Run `dotnet test Klinara.slnx --no-restore` and report the actual totals and warnings. A passing unit suite does not establish integration or end-to-end completion. Check specifically for controller integration tests and Playwright coverage when the plan requires them.
4. For frontend review, inventory all portal pages and reusable components, then follow the actual user flow: email entry → OTP send/validation → doctor/slot retrieval → booking → confirmation/view/cancellation. Verify API calls, dynamic clinic/subdomain context, validation, errors, and loading states; don't describe placeholder UI as integrated functionality.
5. Compare the observed state to acceptance criteria and report only the remaining work, grouped by sprint or priority. Flag plan sections that are stale and distinguish an existing configuration file from versioned/deployment configuration.

## Implementation and review rules

- Keep OTP purpose consistent between OTP creation and validation; test both sides of the flow.
- When validating OTP with `_userManager.VerifyUserTokenAsync`, pass the 6-digit code directly without padding — Identity expects the exact token, not a padded 32-char string. Padding the code causes validation to fail because the reconstructed token doesn't match the stored format.
- Enforce clinic and patient scoping, token expiry/single-use, booking horizon, and strike blocking in server-side services, not only in the UI.
- Treat Razor `RZ10012` unknown-component warnings as functional defects: a successful build can still render an unresolved custom element when its component namespace is missing or wrong.
- Report nullable/compiler warnings explicitly when the Definition of Done requires a warning-free build; passing tests do not erase build warnings.
+**Build verification pattern**: When checking build status, run `dotnet test Klinara.slnx --no-restore` and `dotnet build Klinara.slnx --no-restore -t:Rebuild` as separate checkpoints. File-locking warnings (e.g., "The process cannot access the file because it is being used by another process") are runtime/host issues, not compiler errors. Filter compiler errors with `grep -i "error CS"` to distinguish them from MSB3021/MSB3027 file-copy warnings.
- Refresh plan status when implementation changes so obsolete "missing" lists don't mislead the next review.
+**Plan-vs-PRD-audit reconciliation**: Task-level plan documents (e.g., `task_plan.md`) may mark items as complete based on InMemory EF functionality or local UI states, but the PRD compliance audit (`docs/PRD_COMPLIANCE_AUDIT_*.md`) validates against real SQL Server constraints, FK integrity, clinic scoping, and URL routing. Always cross-reference both: passing `dotnet test` and a clean `dotnet build` do not confirm SQL Server FK validity, clinic-isolated authorization, or confirmation URL correctness with subdomain. Flag any plan section as 🟡/❌ in the audit until real-database verification passes.
+**Privacy notice implementation**: Add "Aviso de privacidad" links (shield icon) to footers in both `NavMenu.razor` (admin UI) and `PatientPortalLayout.razor` (patient portal), navigating to a dedicated `/privacy` route. Create a `Privacy.razor` page under `Klinara.Web/Components/PatientPortal/Pages/` with the full privacy notice (5 ARCO sections: data categories, finalities, transfers, security, ARCO rights with email procedure to `privacidad@klinara.com`). The link should open in a new tab (`target="_blank"`).
- **OTP generation must use only `EmailVerification` purpose** — do not generate a `Login` OTP first and then an `EmailVerification` OTP; a single `EmailVerification` code is emitted per portal verification request. The test `SendVerificationEmailAsync_GeneratesOnlyEmailVerificationOtp` validates this guardrail.
- Do **not** add a unique index on `{ClinicId, Type, PatientId}`: token history persists after use, and patients must be able to receive multiple tokens of each type over time. The existing unique index on the random token value is appropriate; enforce active-token policy explicitly only if product requirements demand it.
- **User preference: deliverables should be concise and direct, matching the weight of the ask without filler or restatements.** No «Great question,» no restating the request, no re-summarizing what already was said. Plain claims over adjectives; when unsure, say so plainly.

- **PRD compliance verification**: When reviewing PRD compliance, always validate both test suite results (`dotnet test --no-restore`) and build status (`dotnet build --no-restore -t:Rebuild`) as separate checkpoints. A passing test suite does not establish build purity, and a clean build does not confirm test coverage. Report both states in any compliance summary.

- **CreatedByUserId FK constraint**: In `PatientPortalService.BookAppointmentAsync`, the `Appointment.CreatedByUserId` must reference an `ApplicationUser` (Identity user), not a `Patient`. The portal creates a shadow `ApplicationUser` during OTP verification (`SendVerificationEmailAsync`); use that user's `Id` when persisting the appointment. Using `Patient.Id` violates the FK and fails on real SQL Server (InMemory EF does not enforce FKs).

- **Confirmation email URL must include clinic subdomain**: The confirmation link sent to patients must follow the route pattern `/portal/{subdomain}/confirm/{token}`. Building the URL without the subdomain (`/confirm/{token}`) produces a 404 because the Blazor route requires the subdomain segment. Retrieve the subdomain from `appointment.Clinic?.Subdomain` when constructing the email.

- **Nullable test helpers**: The `Patient.Email` property is nullable (`string?`). Test helper methods like `CreatePatient` must accept `string? email` to avoid CS8604 warnings. When fixing nullable warnings in test files, update the helper signatures — do not suppress warnings.

- **Token security in URLs**: Bearer tokens in route paths (`/appointment/{token}`, `/cancel/{token}`, `/confirm/{token}`) are logged by browsers, proxies, and server access logs. Production deployment must add: `Referrer-Policy: strict-origin-when-cross-origin` (or `no-referrer`), secure headers via `app.UseHsts()`, and structured log redaction for token values. The audit treats this as a functional blocker.

- **Build warnings are release gates**: The PRD Definition of Done requires "Build 0 errores / 0 warnings". Compiler warnings (especially CS8602/8604 nullable) must be fixed before tagging a release. A clean test suite does not erase build warnings.

- **Clinic isolation in controllers (multi-tenant SaaS pattern)**: Every controller accepting `ClinicId` from the request MUST validate it against the authenticated user's `ClinicId` claim. Pattern:
  ```csharp
  private Guid? GetCurrentUserClinicId()
  {
      var claim = User.FindFirst("ClinicId")?.Value;
      return Guid.TryParse(claim, out var id) ? id : null;
  }
  
  // In action:
  var userClinicId = GetCurrentUserClinicId();
  if (userClinicId.HasValue && request.ClinicId != userClinicId.Value)
      return Forbid(); // PlatformAdmin (ClinicId=null) bypasses
  ```
  Apply to ALL clinic-scoped endpoints: payments, patients, appointments, schedules. PlatformAdmin (`ClinicId=null`) sees all; ClinicAdmin/Doctor/Employee only their clinic.

- **Service-level clinic filtering**: Extend service methods with optional `requestingClinicId` parameter. When provided, filter queries by `ClinicId`. Controllers pass the user's clinic ID; PlatformAdmin passes `null`. Example:
  ```csharp
  public async Task<PatientBalanceDto> GetPatientBalanceAsync(Guid patientId, Guid? requestingClinicId = null)
  {
      var query = _context.Patients.Where(p => p.Id == patientId);
      if (requestingClinicId.HasValue) query = query.Where(p => p.ClinicId == requestingClinicId.Value);
      // ...
  }
  ```
  This pushes isolation to the service layer, not just controllers.

- **Seed demo data in RoleSeeder**: Add idempotent seed logic for Platform Admin, Clinic, Doctor, and Employee in `RoleSeeder.SeedAsync`. Use `UserManager.FindByEmailAsync` to check existence before creating. Platform Admin gets `ClinicId = null` and `AdminPlataforma` role; Clinic-scoped users get their `ClinicId` set. All demo users use password `Password.321`. This runs automatically on app startup via `Program.cs`.

- **Playwright E2E project structure**: Create separate `Klinara.E2E` xUnit project with `Microsoft.Playwright` and `Microsoft.Playwright.Xunit`. Use `_playwright.APIRequest.NewContextAsync()` for API tests (not `_page.Request`). Run Chromium headless via `playwright install chromium`. E2E tests require the app running; exclude them from unit test runs with `--filter "FullyQualifiedName!~Klinara.E2E"`.

- **Unit test isolation**: When adding E2E project, ensure `dotnet test --filter "FullyQualifiedName!~Klinara.E2E"` runs only unit tests. E2E tests that hit `net::ERR_CONNECTION_REFUSED` are expected when the app isn't running — this is not a test failure but an environment prerequisite.

- **Spanish localization of Account pages**: Translate English UI texts in Account area to Spanish:
  - `ChangePassword.razor`: "Old password" → "Contraseña antigua", "New password" → "Nueva contraseña", "Confirm password" → "Confirmar contraseña"
  - `DeletePersonalData.razor`: "Please enter your password." → "Ingrese su contraseña", "Password" → "Contraseña"
  - `Email.razor`: "Enter your email" → "Ingrese su email" (2 occurrences), "New email" → "Nuevo email"
  - `Register.razor`: "Confirm Password" → "Confirmar contraseña"
  - `LoginWithRecoveryCode.razor`: "RecoveryCode" → "Código de recuperación"
  - `ResetPassword.razor`: "Please enter your password." → "Ingrese su contraseña", "Please confirm your password." → "Confirme su contraseña"

## Project landmarks

Backend portal work centers on `PatientTokenService`, `IPatientPortalService` / `PatientPortalService`, `PatientPortalController`, `ClinicSubdomainMiddleware`, `AppointmentService`, and `ErrorCodes`. Blazor portal components live under `Klinara.Web/Components/PatientPortal`; tests are in `Klinara.Tests`. The solution's portal plan is `docs/PLAN_FASE3_PATIENT_PORTAL.md`.

---

## Klinara-specific patterns (Nov 2026)

### NavMenu authorization con roles condicionales
```razor
<AuthorizeView>
    <Authorized>
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="staff/patients">Pacientes</NavLink>
        </div>
        @if (IsPlatformAdmin())
        {
            <div class="nav-item px-3">
                <NavLink class="nav-link" href="Account/Manage">Configuración</NavLink>
            </div>
        }
        <div class="nav-item px-3">
            <form action="Account/Logout" method="post">
                <AntiforgeryToken />
                <input type="hidden" name="ReturnUrl" value="~/" />
                <button type="submit" class="nav-link">
                    <span class="bi bi-box-arrow-right me-2" aria-hidden="true"></span> Cerrar sesión
                </button>
            </form>
        </div>
    </Authorized>
</AuthorizeView>

@code {
    private bool IsPlatformAdmin()
    {
        var authState = AuthenticationStateProvider.GetAuthenticationStateAsync().Result;
        return authState.User.IsInRole(KlinaraRoles.PlatformAdmin);
    }
}
```
- Usar `AuthorizeView` con `@if (IsPlatformAdmin())` para elementos solo PlatformAdmin
- Helper `IsPlatformAdmin()` usa `AuthenticationStateProvider` sincrónico (`.Result`) — aceptable en render; para async real, usar `<AuthorizeView Policy="PlatformAdminOnly">`
- **Logout button**: form POST a `Account/Logout` con `ReturnUrl="~/"` (no `/`); `"/"` lanza `InvalidOperationException: The supplied URL is not local`; `"~/"` es ruta virtual explícitamente local
- `AntiforgeryToken` requerido para POST

### TDD para UserAdministrationService
- Escribir tests primero (`UserAdministrationServiceTests.cs`) con fixture `TestFixture` que configura InMemory EF + Identity
- Tests cubren: PlatformAdmin cambia rol/clínica, ClinicAdmin restringido a su clínica, protección último PlatformAdmin, Employee sin permisos, ListUsersAsync filtrado por rol
- Implementación mínima en `UserAdministrationService.ChangeRoleAsync` y `ListUsersAsync` validando claims del actor

### Auditoría de cumplimiento PRD
- Documento `docs/PRD_COMPLIANCE.md` actualizado tras cada fase
- Encabezado con enlace a auditoría detallada: `> **Estado vigente:** consulta la [auditoría actualizada](PRD_COMPLIANCE_AUDIT_YYYY-MM-DD.md)`
- Checklist por fase con ✅/🟡/❌/⚠️ y columna "Evidencia"
- Resumen ejecutivo con % por fase y flujos de aceptación MVP
- Prioridades numeradas para cerrar brechas

### Migración EF Core Identity limpia (Guid)
- Ver §1b en `controlzeta-development`: `dotnet ef database drop --force` → eliminar migraciones → `migrations add InitialCreate` → `database update`
- El scaffold respeta `IdentityUser<Guid>` y crea todas las tablas Identity con `uniqueidentifier` directo
- Evitar `AlterColumn string → Guid` en tablas Identity con PK/FK existentes

### Privacy notice footer links (Nov 2026)
- Add "Aviso de privacidad" link (shield icon) to `NavMenu.razor` footer, visible in both Authorized and NotAuthorized views, navigating to `/privacy` in a new tab
- Add "Aviso de privacidad" link (shield icon) to `PatientPortalLayout.razor` footer, navigating to `/privacy` in a new tab
- Create `Privacy.razor` page under `Klinara.Web/Components/PatientPortal/Pages/` with full privacy notice (data categories, finalities, transfers, security, ARCO rights with email procedure to `privacidad@klinara.com`)

### Build warning filtering (Nov 2026)
- Use `dotnet build --no-restore -t:Rebuild 2>&1 | grep -i "error CS"` to isolate compiler warnings from MSB3021/MSB3027 file-copy warnings caused by running web host processes
- A clean `grep -i "error CS"` exit code does not guarantee zero warnings — it only confirms zero compiler errors. Always also check for `warn` patterns separately if a warning-free build is a release gate.
