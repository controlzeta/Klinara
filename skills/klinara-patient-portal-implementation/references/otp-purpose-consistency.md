# OTP Purpose Consistency

## Purpose Usage Table

| Purpose | Use Case | Supported by |
|---------|----------|--------------|
| `OtpPurposes.Login` | Login OTP, email verification portal flow, password reset | `OtpEmailSender`, `IOtpService` |
| `OtpPurposes.EmailVerification` | arXiv/documentation reference only | Not directly supported by `OtpEmailSender` |

## Key Rule

**Never mix `OtpPurposes.EmailVerification` and `OtpPurposes.Login` in the same service flow.** The `OtpEmailSender` class (defined in `Klinara.Web/Services/EmailSender.cs`) only generates and validates OTPs with `OtpPurposes.Login`. If the patient portal service previously used `OtpPurposes.EmailVerification` with `_otpService.GenerateOtpAsync()`, this was inconsistent and has been fixed by switching to `_otpEmailSender.SendLoginOtpAsync()` and using the returned OTP code directly.

## Migration Path

1. Find all calls to `_otpService.GenerateOtpAsync(user, OtpPurposes.EmailVerification)` 
2. Replace with `_otpEmailSender.SendLoginOtpAsync(user)` 
3. Use the returned OTP code directly — do not pass through `OtpPurposes.EmailVerification`
4. Build and verify: `dotnet build` and `dotnet test --no-build`

## Pitfall

Attempting to use `OtpPurposes.EmailVerification` with `OtpService` directly while also using `OtpEmailSender` creates two parallel OTP code paths. This was the code smell fixed in this session: consolidate to a single purpose (`Login`) throughout the portal service.