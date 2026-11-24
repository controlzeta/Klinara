---
name: code-smell-fixes-dotnet
description: Procedures for fixing code smells, centralizing error codes, and verifying build/tests in .NET/Blazor projects like Klinara
version: 1.0.0
author: Francisco Arroyo
license: MIT
platforms: [windows, linux]
metadata:
  hermes:
    tags: [code-smells, dotnet, blazor, otp, error-codes]
    category: backend
---
# Code Smell Fixes & Build Verification for .NET/Blazor Projects

## Triggers
- Build fails with CS or RC errors after modifying service classes
- Tests pass but code quality degrades (magic strings, duplicated logic)
- New .razor components fail to compile due to missing imports/types
- OTP/email sending inconsistencies surface in patient portal work

## Always-On Rules (in SKILL.md)
1. **User prefers concise responses**: No filler phrases ('Great question,' 'I'd be happy to,' etc.), no restating the request, no narrating tool calls. Plain claims over adjectives. When unsure, say so plainly.
2. **Build must succeed with 0 errors / 0 warnings**: Every commit must pass `dotnet build` cleanly. If build fails, stop and fix before proceeding.
3. **80 tests minimum passing**: Sprint 1 baseline is 80/80 PatientTokenServiceTests (14) + PatientPortalServiceTests (16) + AppointmentServiceTests (4 + original 46). Do not reduce test coverage.
4. **Patient portal works without staff login**: Email → OTP → token → reserva → confirmacion. All endpoints and pages marked [AllowAnonymous] for unauthenticated access.
5. **Horizonte 24h min / 45d máx validado server-side**: Schedule validation in AppointmentService with `isPatientBooking=true`.
6. **Tokens single-use con expiración**: PortalAccess (30 min, single-use), AppointmentConfirmation (24h, single-use). Consumed on ValidateAndConsumeTokenAsync / ValidateAndConsumeTokenAsync.
7. **Design system variables in CSS**: `--primary, --surface, --foreground, --muted-foreground, --accent, --border`. All new styles must use these variables.
8. **Subdomain-to-ClinicId mapping**: ClinicSubdomainMiddleware extracts subdomain from URL path and stores ClinicId in HttpContext.Items for downstream services.
9. **No API keys, tokens, passwords, or credentials in summaries**: Replace any that appear with `[REDACTED]`.

## Procedures

### SendVerificationEmail Flow
1. Find clinic by subdomain: `var clinic = await _context.Clinics.FirstOrDefaultAsync(c => c.Subdomain == clinicSubdomain && c.IsActive);`
2. Find or create patient by email in that clinic
3. Check if patient is blocked (`patient.IsBlocked && patient.BlockedUntil.HasValue && patient.BlockedUntil.Value > DateTime.UtcNow`)
4. Find or create ApplicationUser for OTP delivery
5. Generate OTP via `_otpEmailSender.SendLoginOtpAsync(portalUser)` — **USE OtpPurposes.Login CONSISTENTLY**, do NOT mix with `OtpPurposes.EmailVerification` in the same flow
6. Send OTP email via `_emailSender.SendPasswordResetCodeAsync(user, email, otpCode)`
7. Return `PortalVerificationResult(true, null, email)`

**Pitfall**: Mixing `OtpPurposes.EmailVerification` and `OtpPurposes.Login` in the same service. The `OtpEmailSender` only supports `OtpPurposes.Login`. Use one purpose consistently throughout.

### Error Code Centralization
1. Create `Klinara.Web/Services/ErrorCodes.cs` with static constants for all error messages
2. Replace all magic strings in services with `ErrorCodes.Xxx` references
3. Import `Klinara.Web.Services` namespace where ErrorCodes is used
4. Build verify: `dotnet build` must pass with 0 errors

**Pitfall**: Whitespace differences in patch old_string matching. Always read the full file with `read_file` before patching, or use exact whitespace from the file on disk.

### Token Lifecycle
1. **PortalAccess tokens**: 30 min expiry, single-use. Created via `_patientTokenService.CreatePortalAccessTokenAsync(patient.Id, clinic.Id)`. Consumed on `ValidateAndConsumeTokenAsync`. If already consumed, returns null.
2. **AppointmentConfirmation tokens**: 24h expiry, single-use. Created via `_patientTokenService.CreateAppointmentConfirmationTokenAsync(patient.Id, appointment.Id, clinicId)`. Consumed on `ValidateAndConsumeTokenAsync`.
3. **Token validation**: Always use `ValidateAndConsumeTokenAsync` for consumption, `ValidateTokenAsync` for read-only checks.
4. **RevokeAllPatientTokensAsync**: Use to clean up all tokens for a patient when needed.

**Pitfall**: `dotnet test --no-build` exit 1 after new test class creation. Always fix Mock<UserManager> setup with generated token tracking dictionary, and fix assertion logic for horizon/blocked errors before running tests.

### Build & Test Workflow
1. `cd C:/github/Klinara && dotnet build 2>&1` — verify exit 0, 0 warnings
2. `cd C:/github/Klinara && dotnet test --no-build 2>&1` — verify 80/80 passing
3. If tests fail, identify the specific test setup issue (Mock<UserManager> token tracking, assertion logic, test data) and fix before proceeding
4. Never commit broken test code — fix tests first, then commit

**Pitfall**: `dotnet test --no-build` exit 1 after new test class creation. Always fix Mock<UserManager> setup with generated token tracking dictionary, and fix assertion logic for horizon/blocked errors before running tests.

### Sprint 2 Frontend Cleanup
When Blazor `.razor` files have compile errors:

1. Delete the broken component directory
2. Recreate minimal `_Imports.razor` with only essential usings
3. Re-add components one at a time, verifying `dotnet build` after each
4. Add `@using Klinara.Web.Models` where TimeSlot/DayScheduleDto types are used
5. Use `@attribute [AllowAnonymous]` (not custom AuthorizeAnonymous) for public pages

**Pitfall**: 96 compile errors from broken imports/types. Clean slate (delete + recreate) is faster than patching each broken file.

## References
- `references/otp-purpose-consistency.md` — Detailed OTP purpose usage table
- `references/error-codes-reference.md` — Full ErrorCodes constants catalog
- `references/blazor-component-setup.md` — Minimal `.razor` startup pattern

## Skills this Supplements
- No existing skills cover this class of task. This is the foundational skill for Klinara portal implementation.