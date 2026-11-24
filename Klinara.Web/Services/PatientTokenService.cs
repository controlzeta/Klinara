using System;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klinara.Web.Services;

public interface IPatientTokenService
{
    Task<PatientToken> CreatePortalAccessTokenAsync(Guid patientId, Guid clinicId, TimeSpan? expiry = null);
    Task<PatientToken> CreateAppointmentConfirmationTokenAsync(Guid patientId, Guid appointmentId, Guid clinicId, TimeSpan? expiry = null);
    Task<PatientToken> CreateAppointmentReminderTokenAsync(Guid patientId, Guid appointmentId, Guid clinicId, TimeSpan? expiry = null);
    Task<PatientToken> CreateEmailVerificationTokenAsync(Guid patientId, Guid clinicId, TimeSpan? expiry = null);
    Task<PatientToken?> ValidateAndConsumeTokenAsync(string token, PatientTokenType expectedType);
    Task<PatientToken?> ValidateTokenAsync(string token, PatientTokenType expectedType);
    Task InvalidateTokenAsync(Guid tokenId);
    Task<bool> IsTokenValidAsync(string token, PatientTokenType expectedType);
    Task RevokeAllPatientTokensAsync(Guid patientId, PatientTokenType? type = null);
}

public class PatientTokenService : IPatientTokenService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PatientTokenService> _logger;

    // Expiraciones por defecto
    private static readonly TimeSpan DefaultPortalAccessExpiry = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DefaultAppointmentConfirmationExpiry = TimeSpan.FromHours(24);
    private static readonly TimeSpan DefaultAppointmentReminderExpiry = TimeSpan.FromHours(24);
    private static readonly TimeSpan DefaultEmailVerificationExpiry = TimeSpan.FromHours(1);

    public PatientTokenService(ApplicationDbContext context, ILogger<PatientTokenService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PatientToken> CreatePortalAccessTokenAsync(Guid patientId, Guid clinicId, TimeSpan? expiry = null)
    {
        var token = GenerateSecureToken();
        var patientToken = new PatientToken
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            ClinicId = clinicId,
            Type = PatientTokenType.PortalAccess,
            Token = token,
            ExpiresAt = DateTime.UtcNow.Add(expiry ?? DefaultPortalAccessExpiry),
            CreatedAt = DateTime.UtcNow
        };

        _context.PatientTokens.Add(patientToken);
        await _context.SaveChangesAsync();

        _logger.LogInformation("PortalAccess token creado para PatientId: {PatientId}, ClinicId: {ClinicId}, Expira: {ExpiresAt}",
            patientId, clinicId, patientToken.ExpiresAt);

        return patientToken;
    }

    public async Task<PatientToken> CreateAppointmentConfirmationTokenAsync(Guid patientId, Guid appointmentId, Guid clinicId, TimeSpan? expiry = null)
    {
        var token = GenerateSecureToken();
        var patientToken = new PatientToken
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            AppointmentId = appointmentId,
            ClinicId = clinicId,
            Type = PatientTokenType.AppointmentConfirmation,
            Token = token,
            ExpiresAt = DateTime.UtcNow.Add(expiry ?? DefaultAppointmentConfirmationExpiry),
            CreatedAt = DateTime.UtcNow
        };

        _context.PatientTokens.Add(patientToken);
        await _context.SaveChangesAsync();

        _logger.LogInformation("AppointmentConfirmation token creado para PatientId: {PatientId}, AppointmentId: {AppointmentId}, ClinicId: {ClinicId}",
            patientId, appointmentId, clinicId);

        return patientToken;
    }

    public async Task<PatientToken> CreateAppointmentReminderTokenAsync(Guid patientId, Guid appointmentId, Guid clinicId, TimeSpan? expiry = null)
    {
        var token = GenerateSecureToken();
        var patientToken = new PatientToken
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            AppointmentId = appointmentId,
            ClinicId = clinicId,
            Type = PatientTokenType.AppointmentReminder,
            Token = token,
            ExpiresAt = DateTime.UtcNow.Add(expiry ?? DefaultAppointmentReminderExpiry),
            CreatedAt = DateTime.UtcNow
        };

        _context.PatientTokens.Add(patientToken);
        await _context.SaveChangesAsync();

        _logger.LogInformation("AppointmentReminder token creado para PatientId: {PatientId}, AppointmentId: {AppointmentId}",
            patientId, appointmentId);

        return patientToken;
    }

    public async Task<PatientToken> CreateEmailVerificationTokenAsync(Guid patientId, Guid clinicId, TimeSpan? expiry = null)
    {
        var token = GenerateSecureToken();
        var patientToken = new PatientToken
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            ClinicId = clinicId,
            Type = PatientTokenType.EmailVerification,
            Token = token,
            ExpiresAt = DateTime.UtcNow.Add(expiry ?? DefaultEmailVerificationExpiry),
            CreatedAt = DateTime.UtcNow
        };

        _context.PatientTokens.Add(patientToken);
        await _context.SaveChangesAsync();

        _logger.LogInformation("EmailVerification token creado para PatientId: {PatientId}, ClinicId: {ClinicId}",
            patientId, clinicId);

        return patientToken;
    }

    public async Task<PatientToken?> ValidateAndConsumeTokenAsync(string token, PatientTokenType expectedType)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var patientToken = await _context.PatientTokens
            .Include(t => t.Patient)
            .FirstOrDefaultAsync(t => t.Token == token && t.Type == expectedType);

        if (patientToken == null)
        {
            _logger.LogWarning("Token no encontrado: {Token} (Type: {ExpectedType})", token[..Math.Min(8, token.Length)], expectedType);
            return null;
        }

        if (patientToken.UsedAt.HasValue)
        {
            _logger.LogWarning("Token ya usado: {TokenId} (Type: {ExpectedType}, UsedAt: {UsedAt})", patientToken.Id, expectedType, patientToken.UsedAt);
            return null;
        }

        if (patientToken.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Token expirado: {TokenId} (Type: {ExpectedType}, ExpiresAt: {ExpiresAt})", patientToken.Id, expectedType, patientToken.ExpiresAt);
            return null;
        }

        // Marcar como usado (single-use)
        patientToken.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Token validado y consumido: {TokenId} (Type: {ExpectedType}, PatientId: {PatientId})",
            patientToken.Id, expectedType, patientToken.PatientId);

        return patientToken;
    }

    public async Task<PatientToken?> ValidateTokenAsync(string token, PatientTokenType expectedType)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var patientToken = await _context.PatientTokens
            .Include(t => t.Patient)
            .FirstOrDefaultAsync(t => t.Token == token && t.Type == expectedType);

        if (patientToken == null)
        {
            _logger.LogWarning("Token no encontrado: {Token} (Type: {ExpectedType})", token[..Math.Min(8, token.Length)], expectedType);
            return null;
        }

        if (patientToken.UsedAt.HasValue)
        {
            _logger.LogWarning("Token ya usado: {TokenId} (Type: {ExpectedType})", patientToken.Id, expectedType);
            return null;
        }

        if (patientToken.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Token expirado: {TokenId} (Type: {ExpectedType}, ExpiresAt: {ExpiresAt})", patientToken.Id, expectedType, patientToken.ExpiresAt);
            return null;
        }

        // NO marcar como usado (solo validación, no consumo)
        return patientToken;
    }

    public async Task InvalidateTokenAsync(Guid tokenId)
    {
        var token = await _context.PatientTokens.FindAsync(tokenId);
        if (token != null)
        {
            token.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Token invalidado manualmente: {TokenId}", tokenId);
        }
    }

    public async Task<bool> IsTokenValidAsync(string token, PatientTokenType expectedType)
    {
        var patientToken = await ValidateTokenAsync(token, expectedType);
        return patientToken != null;
    }

    public async Task RevokeAllPatientTokensAsync(Guid patientId, PatientTokenType? type = null)
    {
        var query = _context.PatientTokens.Where(t => t.PatientId == patientId && !t.UsedAt.HasValue);

        if (type.HasValue)
            query = query.Where(t => t.Type == type.Value);

        var tokens = await query.ToListAsync();
        foreach (var token in tokens)
        {
            token.UsedAt = DateTime.UtcNow;
        }

        if (tokens.Count > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("{Count} tokens revocados para PatientId: {PatientId} (Type: {Type})",
                tokens.Count, patientId, type?.ToString() ?? "ALL");
        }
    }

    private static string GenerateSecureToken()
    {
        // Generar token URL-safe: 32 bytes = 43 chars base64url
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}