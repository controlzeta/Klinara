# Blazor Component Startup Pattern

## Minimal .razor Import File (`_Imports.razor`)

```razor
@using Klinara.Web
@using Klinara.Web.Services
@using Klinara.Web.Components.Account
```

**Never** include broken or outdated usings. If a component has compile errors, delete the component directory and recreate from this minimal pattern, then add pieces one at a time while verifying `dotnet build` after each change.

## Component Creation Workflow

1. Create `_Imports.razor` with minimal usings above
2. Create layout component (`PatientPortalLayout.razor`) with `[Parameter]` props for ContextClinicSubdomain, ContextPatientName, ContextPatientEmail, ContextPageTitle, ContextShowDemoBadge
3. Create index page (`Pages/Index.razor`) with `@page "/portal/@ContextClinicSubdomain"` and `[AllowAnonymous]`
4. Create verify page (`Pages/VerifyEmail.razor`) with `@page "/portal/@ContextClinicSubdomain/verify"` and `[AllowAnonymous]`
5. Create booking page (`Pages/BookAppointment.razor`) with `@page "/portal/@ContextClinicSubdomain/booking"` and `[AllowAnonymous]`
6. Create components (`Components/DoctorSelector.razor`, `Components/SlotPicker.razor`)
7. Add `@using Klinara.Web.Models` when using TimeSlot, DayScheduleDto, DoctorAvailabilityDto types
8. Verify after each: `dotnet build 2>&1 | grep -E "(Error|Compilaci[oó]n correcta)"`

## Common compile-error fixes

| Error | Fix |
|-------|-----|
| `CS0103: El nombre 'context' no existe en el contexto actual` | Check `@inherits` tag — should be `LayoutComponentBase` or correct base type |
| `CS0103: El nombre 'set' no existe en el contexto actual` | Add `@using Klinara.Web.Models` for TimeSlot types |
| `CS1061: "DoctorAvailabilityDto" no contiene una definición para "Value"` | Add `@using Klinara.Web.Services`; ensure DoctorAvailabilityDto has public Value property |
| `CS7014: Los atributos no son válidos en este contexto` | Remove invalid `@attribute` declarations from `.razor` code blocks |
| `CS0841: No se puede usar la variable local 'AvailableSlots' antes de declararla` | Ensure `@foreach (var slot in AvailableSlots)` is preceded by `[Parameter] public List<TimeSlot> AvailableSlots { get; set; } = new();` |

## Pitfall

Adding all possible `@using` directives at once rather than incrementally. Each new component or type should be added one at a time with a build verification step. This makes it easy to identify exactly which import fixes the compilation.

## Verification

After each component addition or import change:

```bash
cd C:/github/Klinara
dotnet build 2>&1 | tail -5
# Expected: "Compilación correcta." with "0 Errores", "0 Advertencia(s)"
```