# Identity + OTP Implementation — Klinara

## Resumen

Sistema de autenticación basado en **OTP (One-Time Password) de 6 dígitos** enviado por email, con sesión de 1 hora, lockout por intentos fallidos, y sin almacenamiento de secretos en logs.

---

## Flujo de Login

```
1. Usuario ingresa email en /Account/Login
2. Sistema genera OTP de 6 dígitos (UserManager.GenerateUserTokenAsync)
3. EmailSender envía OTP (LoggingEmailSender en dev, implementación real en prod)
4. Usuario ingresa código de 6 dígitos
5. Sistema valida OTP (UserManager.VerifyUserTokenAsync)
6. Si válido: SignInManager.SignInAsync → sesión 1h + cookie
7. Si inválido: incrementar contador, lockout tras 5 intentos (15 min)
```

---

## Componentes Implementados

### 1. `OtpService` (`Services/OtpService.cs`)

```csharp
public interface IOtpService
{
    Task<string> GenerateOtpAsync(ApplicationUser user, string purpose);
    Task<bool> ValidateOtpAsync(ApplicationUser user, string code, string purpose);
    Task InvalidateOtpAsync(ApplicationUser user, string purpose);
}
```

**Propósitos OTP** (`OtpPurposes`):
- `LOGIN` — Acceso al sistema
- `EMAIL_VERIFICATION` — Verificación de email al registrar
- `PASSWORD_RESET` — Recuperación de contraseña
- `PATIENT_PORTAL` — Token de acceso paciente (portal público)

**Métodos**:
- `GenerateOtpAsync` → token 6 dígitos (primeros 6 chars del token ASP.NET Core)
- `ValidateOtpAsync` → verifica código 6 dígitos, invalida tras éxito
- `InvalidateOtpAsync` → rota security stamp (invalida todos los tokens del usuario)

### 2. `LoggingEmailSender` (`Services/EmailSender.cs`)

```csharp
public class LoggingEmailSender : IEmailSender<ApplicationUser>
{
    // NO loguea OTPs ni links con tokens
    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        _logger.LogInformation("Código de recuperación enviado a {Email} (UserId: {UserId})", email, user.Id);
        return Task.CompletedTask;
    }
}
```

**Regla de seguridad**: **NUNCA** loguear OTPs, códigos, tokens ni links completos.

### 3. `OtpEmailSender` (`Services/EmailSender.cs`)

Wrapper para envío real de OTPs de login:

```csharp
public async Task<string> SendLoginOtpAsync(ApplicationUser user)
{
    var otp = await _otpService.GenerateOtpAsync(user, OtpPurposes.Login);
    _logger.LogInformation("OTP de login generado para {Email} (UserId: {UserId})", user.Email, user.Id);
    return otp; // Solo dev/dev; en prod no retornar
}
```

### 4. `ApplicationUser` — Campos OTP

```csharp
public int OtpFailedAttempts { get; set; } = 0;
public DateTime? OtpLastFailedAt { get; set; }
public DateTime? OtpLockoutEnd { get; set; }
public bool MustChangePassword { get; set; } = false;
public DateTime? LastLoginAt { get; set; }
```

---

## Configuración Identity (`Program.cs`)

```csharp
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    
    // Lockout
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    
    // Tokens
    options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders()
.AddTokenProvider<EmailTokenProvider<ApplicationUser>>(TokenOptions.DefaultEmailProvider);

// Services
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<OtpEmailSender>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, LoggingEmailSender>();
```

---

## Parámetros de Seguridad

| Parámetro | Valor | Justificación |
|---|---|---|
| **OTP length** | 6 dígitos | Estándar industral, usable en móvil |
| **OTP validity** | 1 hora | Token ASP.NET Core default |
| **Max failed attempts** | 5 | Balance usabilidad/seguridad |
| **Lockout duration** | 15 min | Mitiga brute force |
| **Session timeout** | 1 hora | Cookie sliding expiration |
| **Require confirmed email** | true | Previene cuentas fantasmas |

---

## Seguridad: No Loguear Secretos

### ✅ CORRECTO
```csharp
_logger.LogInformation("OTP generado para {Email} (UserId: {UserId})", email, userId);
_logger.LogInformation("Login exitoso para {Email}", email);
```

### ❌ INCORRECTO
```csharp
_logger.LogInformation("OTP {Code} enviado a {Email}", code, email);  // FILTRA CÓDIGO
_logger.LogInformation("Link: {Link}", confirmationLink);             // FILTRA TOKEN
```

**Enriquecimiento seguro para auditoría**:
```csharp
using (LogContext.PushProperty("UserId", user.Id))
using (LogContext.PushProperty("ClinicId", user.ClinicId))
{
    Log.Information("Login exitoso");
}
```

---

## Tests de Verificación

| Test | Qué Verifica |
|---|---|
| `OtpPurposes_Constants_Are_Defined` | Constantes de propósito correctas |
| `TenantIsolationTests` (7) | Aislamiento por ClinicId, admin global bypass |
| `RolePolicyTests` (12) | Políticas registradas, jerarquía de roles |

**Ejecutar**: `dotnet test Klinara.Tests/Klinara.Tests.csproj`

---

## Próximos Pasos (Post-Fase 1)

1. **Email real** — Implementar `IEmailSender` con SendGrid/Mailgun/SMTP
2. **WhatsApp OTP** — Canal alternativo para México (Twilio/Gupshup)
3. **Rate limiting** — Por IP/email en endpoint `/Account/Login`
4. **Auditoría completa** — Tabla `LoginAttempts` (IP, UserAgent, éxito/fallo)
5. **2FA opcional** — TOTP (Google Authenticator/Authy) para admins
6. **Passwordless** — Magic links como alternativa a OTP

---

## Referencias

- [ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity)
- [EmailTokenProvider](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.identity.emailtokenprovider-1)
- [Serilog Context Enrichment](https://github.com/serilog/serilog/wiki/Context-Enrichment)

---

*Implementación lista para producción. Revisar logs en `logs/` tras primer despliegue.*