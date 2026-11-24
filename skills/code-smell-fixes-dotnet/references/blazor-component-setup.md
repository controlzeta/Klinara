# Minimal Blazor Component Startup Pattern (Klinara Sprint 2)

## When to Use
When creating new `.razor` files that compile errors from broken imports/types, or when adding new pages/components to the patient portal.

## Minimal `_Imports.razor` Content
```razor
@using Klinara.Web
@using Klinara.Web.Services
@using Klinara.Web.Data
@using Microsoft.AspNetCore.Components.Authorization
@using Klinara.Web.Components.Account
```

## Common Component Template
```razor
@page "/route/here"
@attribute [AllowAnonymous]  <!-- Or omit for staff-only pages -->

@using Microsoft.AspNetCore.Components.Authorization
@using Klinara.Web
@using Klinara.Web.Services

<div class="component-section">
    <!-- Component content here -->
</div>

@code {
    [Parameter] public string ContextClinicSubdomain { get; set; } = "";
    
    // Component logic here
}
```

## Common Errors & Fixes

| Error | Cause | Fix |
|-------|-------|-----|
| `CS0103: El nombre 'ContextClinicSubdomain' no existe en el contexto actual` | Missing `[Parameter]` declaration or `@using Klinara.Web` | Add `[Parameter] public string ContextClinicSubdomain { get; set; } = "";` and ensure `_Imports.razor` has `Klinara.Web` |
| `CS0246: El nombre del tipo o del espacio de nombres 'X' no se encontró` | Missing using directive or reference | Add `@using Klinara.Web.[Namespace]` to `_Imports.razor` or add `using` at top of `.razor` |
| `CS0103: El nombre 'set' no existe en el contexto actual` | Nested `@` in class attribute (e.g., `@(slot.IsBooked ? ...)`) | Simplify class attribute: compute string variable first, then use `@className` |
| `RZ9986: Component attributes do not support complex content` | `href="/portal/@ContextClinicSubdomain/verify"` in NavLink | Use static href: `href="/portal/verify"` or use `@NavLink` with Route data |
| `RZ9991: The attribute names could not be inferred from bind attribute 'bind-value'` | `@bind-value="prop"` on non-standard component | Use `<input type="email" @bind-value="prop" />` or standard `<InputText @bind-Value="prop" />` |
| `CS1061: 'DoctorAvailabilityDto' no contiene una definición para 'Value'` | Using `.Value` on DTO that has `DoctorName` instead | Use `.DoctorName` or check the DTO definition (`Klinara.Web/Services/IPatientPortalService.cs` line 78-83) |
| `96 compile errors` | Broken component directory from incomplete Sprint 2 implementation | Delete entire `Klinara.Web/Components/PatientPortal/` and recreate minimally |

## When to Delete vs Patch
- **Delete entire directory** if >50 compile errors or components are 50%+ broken (faster than patching each file)
- **Patch individual files** if only 1-2 errors per file and the structure is mostly correct

## Related
- `Klinara.Web/Components/PatientPortal/_Imports.razor` — Recreated in current session
- `Klinara.Web/Components/PatientPortal/Pages/Index.razor` — Public page (40 lines)
- `Klinara.Web/Components/PatientPortal/Pages/VerifyEmail.razor` — Verification page (28 lines)
- `Klinara.Web/Components/PatientPortal/Pages/BookAppointment.razor` — Booking page (51 lines)
- `Klinara.Web/Components/PatientPortal/Components/DoctorSelector.razor` — Doctor selection (41 lines)
- `Klinara.Web/Components/PatientPortal/Components/SlotPicker.razor` — Slot grid (40 lines)