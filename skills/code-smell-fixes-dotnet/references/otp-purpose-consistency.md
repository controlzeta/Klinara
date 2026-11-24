# OTP Purpose Consistency in Klinara

## Purpose Usage Table

| Purpose | Supported by | Usage Pattern |
|---------|-------------|---------------|
| `OtpPurposes.Login` | `OtpEmailSender.SendLoginOtpAsync()` | **ALWAYS USE THIS** with `OtpEmailSender` |
| `OtpPurposes.EmailVerification` | `OtpService.GenerateOtpAsync()` | Only if `OtpEmailSender` also supports it (it doesn't) |

## Rule
Always use `OtpPurposes.Login` with `OtpEmailSender.SendLoginOtpAsync()`. Do NOT mix `OtpPurposes.EmailVerification` directly with `OtpService.GenerateOtpAsync()` in the same flow unless `OtpEmailSender` supports that purpose.

## Why
The `OtpEmailSender` class (in `Klinara.Web/Services/EmailSender.cs`) only implements `OtpPurposes.Login`. Using `OtpPurposes.EmailVerification` with `OtpService` creates inconsistency and compilation errors when the sender doesn't support that purpose.

## Fix Pattern
```csharp
// Before (inconsistent - causes compile errors):
var otp = await _otpService.GenerateOtpAsync(user, OtpPurposes.EmailVerification);

// After (consistent - compiles and works):
var otp = await _otpEmailSender.SendLoginOtpAsync(user);
```

## When to Apply
- Any service method that sends OTP for email verification (e.g., `SendVerificationEmailAsync`)
- Code reviews for new features involving OTP flows
- When adding new OtpPurposes enum values

## Related
- `Klinara.Web/Services/EmailSender.cs` — Only supports `OtpPurposes.Login`
- `Klinara.Web/Services/PatientPortalService.cs` — Was fixed in current session (lines 108-113)
- `Klinara.Web/Services/ErrorCodes.cs` — New file created to centralize error messages