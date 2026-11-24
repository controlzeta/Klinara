using System;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Klinara.Tests;

public class PatientTokenServiceTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Clinic CreateClinic(string name, string subdomain)
    {
        return new Clinic
        {
            Name = name,
            Subdomain = subdomain,
            ContactEmail = $"admin@{subdomain}.com",
            IsActive = true
        };
    }

    private static ApplicationUser CreateUser(string email, Guid? clinicId = null)
    {
        return new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            ClinicId = clinicId
        };
    }

    private static Patient CreatePatient(Guid clinicId, Guid userId, string name, string email)
    {
        return new Patient
        {
            Id = userId,
            ClinicId = clinicId,
            FullName = name,
            Email = email,
            CreatedByUserId = userId
        };
    }

    private static PatientTokenService CreateTokenService(ApplicationDbContext context)
    {
        var logger = new LoggerFactory().CreateLogger<PatientTokenService>();
        return new PatientTokenService(context, logger);
    }

    [Fact]
    public async Task CreatePortalAccessTokenAsync_CreatesTokenWithCorrectProperties()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreatePortalAccessTokenAsync_CreatesTokenWithCorrectProperties));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);

        // Act
        var token = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);

        // Assert
        Assert.NotNull(token);
        Assert.Equal(PatientTokenType.PortalAccess, token.Type);
        Assert.Equal(patient.Id, token.PatientId);
        Assert.Equal(clinic.Id, token.ClinicId);
        Assert.False(string.IsNullOrWhiteSpace(token.Token));
        Assert.True(token.ExpiresAt > DateTime.UtcNow);
        Assert.Null(token.UsedAt);
        Assert.Null(token.AppointmentId);
    }

    [Fact]
    public async Task CreatePortalAccessTokenAsync_DefaultExpiry30Minutes()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreatePortalAccessTokenAsync_DefaultExpiry30Minutes));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);

        // Act
        var before = DateTime.UtcNow;
        var token = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);
        var after = DateTime.UtcNow;

        // Assert
        var expectedMin = before.AddMinutes(29);
        var expectedMax = after.AddMinutes(31);
        Assert.True(token.ExpiresAt >= expectedMin && token.ExpiresAt <= expectedMax);
    }

    [Fact]
    public async Task CreatePortalAccessTokenAsync_CustomExpiry()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreatePortalAccessTokenAsync_CustomExpiry));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);

        // Act
        var customExpiry = TimeSpan.FromHours(2);
        var token = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id, customExpiry);

        // Assert
        var expectedMin = DateTime.UtcNow.AddHours(1).AddMinutes(55);
        var expectedMax = DateTime.UtcNow.AddHours(2).AddMinutes(5);
        Assert.True(token.ExpiresAt >= expectedMin && token.ExpiresAt <= expectedMax);
    }

    [Fact]
    public async Task CreateAppointmentConfirmationTokenAsync_CreatesTokenWithAppointmentId()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAppointmentConfirmationTokenAsync_CreatesTokenWithAppointmentId));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        var appointmentId = Guid.NewGuid();

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);

        // Act
        var token = await service.CreateAppointmentConfirmationTokenAsync(patient.Id, appointmentId, clinic.Id);

        // Assert
        Assert.NotNull(token);
        Assert.Equal(PatientTokenType.AppointmentConfirmation, token.Type);
        Assert.Equal(appointmentId, token.AppointmentId);
        Assert.Equal(clinic.Id, token.ClinicId);
    }

    [Fact]
    public async Task CreateEmailVerificationTokenAsync_CreatesTokenWith1HourExpiry()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateEmailVerificationTokenAsync_CreatesTokenWith1HourExpiry));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);

        // Act
        var token = await service.CreateEmailVerificationTokenAsync(patient.Id, clinic.Id);

        // Assert
        Assert.Equal(PatientTokenType.EmailVerification, token.Type);
        var expectedMin = DateTime.UtcNow.AddMinutes(55);
        var expectedMax = DateTime.UtcNow.AddHours(1).AddMinutes(5);
        Assert.True(token.ExpiresAt >= expectedMin && token.ExpiresAt <= expectedMax);
    }

    [Fact]
    public async Task ValidateAndConsumeTokenAsync_ValidToken_ReturnsTokenAndMarksUsed()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateAndConsumeTokenAsync_ValidToken_ReturnsTokenAndMarksUsed));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);
        var createdToken = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);

        // Act
        var result = await service.ValidateAndConsumeTokenAsync(createdToken.Token, PatientTokenType.PortalAccess);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createdToken.Token, result.Token);
        Assert.NotNull(result.UsedAt);
        Assert.True(result.UsedAt.Value <= DateTime.UtcNow);
    }

    [Fact]
    public async Task ValidateAndConsumeTokenAsync_AlreadyUsedToken_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateAndConsumeTokenAsync_AlreadyUsedToken_ReturnsNull));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);
        var createdToken = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);

        // First validation (consumes)
        await service.ValidateAndConsumeTokenAsync(createdToken.Token, PatientTokenType.PortalAccess);

        // Act - Second validation
        var result = await service.ValidateAndConsumeTokenAsync(createdToken.Token, PatientTokenType.PortalAccess);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAndConsumeTokenAsync_ExpiredToken_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateAndConsumeTokenAsync_ExpiredToken_ReturnsNull));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);

        // Create token with past expiry
        var expiredToken = new PatientToken
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ClinicId = clinic.Id,
            Type = PatientTokenType.PortalAccess,
            Token = "expired-token-123",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow.AddMinutes(-20)
        };
        context.PatientTokens.Add(expiredToken);
        await context.SaveChangesAsync();

        // Act
        var result = await service.ValidateAndConsumeTokenAsync(expiredToken.Token, PatientTokenType.PortalAccess);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAndConsumeTokenAsync_WrongType_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateAndConsumeTokenAsync_WrongType_ReturnsNull));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);
        var createdToken = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);

        // Act - Validate with wrong type
        var result = await service.ValidateAndConsumeTokenAsync(createdToken.Token, PatientTokenType.AppointmentConfirmation);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_ValidToken_ReturnsTokenWithoutMarkingUsed()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateTokenAsync_ValidToken_ReturnsTokenWithoutMarkingUsed));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);
        var createdToken = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);

        // Act
        var result = await service.ValidateTokenAsync(createdToken.Token, PatientTokenType.PortalAccess);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.UsedAt); // No se marca como usado

        // Verificar que se puede validar otra vez
        var result2 = await service.ValidateTokenAsync(createdToken.Token, PatientTokenType.PortalAccess);
        Assert.NotNull(result2);
    }

    [Fact]
    public async Task InvalidateTokenAsync_MarksTokenAsUsed()
    {
        // Arrange
        await using var context = CreateContext(nameof(InvalidateTokenAsync_MarksTokenAsUsed));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);
        var createdToken = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);

        // Act
        await service.InvalidateTokenAsync(createdToken.Id);

        // Assert
        var result = await service.ValidateTokenAsync(createdToken.Token, PatientTokenType.PortalAccess);
        Assert.Null(result);
    }

    [Fact]
    public async Task IsTokenValidAsync_ValidToken_ReturnsTrue()
    {
        // Arrange
        await using var context = CreateContext(nameof(IsTokenValidAsync_ValidToken_ReturnsTrue));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);
        var createdToken = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);

        // Act
        var isValid = await service.IsTokenValidAsync(createdToken.Token, PatientTokenType.PortalAccess);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task RevokeAllPatientTokensAsync_RevokesAllUnusedTokens()
    {
        // Arrange
        await using var context = CreateContext(nameof(RevokeAllPatientTokensAsync_RevokesAllUnusedTokens));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);

        // Crear varios tokens
        var token1 = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);
        var token2 = await service.CreateAppointmentConfirmationTokenAsync(patient.Id, Guid.NewGuid(), clinic.Id);
        var token3 = await service.CreateEmailVerificationTokenAsync(patient.Id, clinic.Id);

        // Act
        await service.RevokeAllPatientTokensAsync(patient.Id);

        // Assert
        Assert.Null(await service.ValidateTokenAsync(token1.Token, PatientTokenType.PortalAccess));
        Assert.Null(await service.ValidateTokenAsync(token2.Token, PatientTokenType.AppointmentConfirmation));
        Assert.Null(await service.ValidateTokenAsync(token3.Token, PatientTokenType.EmailVerification));
    }

    [Fact]
    public async Task RevokeAllPatientTokensAsync_WithTypeFilter_RevokesOnlyThatType()
    {
        // Arrange
        await using var context = CreateContext(nameof(RevokeAllPatientTokensAsync_WithTypeFilter_RevokesOnlyThatType));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);

        var token1 = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);
        var token2 = await service.CreateAppointmentConfirmationTokenAsync(patient.Id, Guid.NewGuid(), clinic.Id);

        // Act - Revocar solo PortalAccess
        await service.RevokeAllPatientTokensAsync(patient.Id, PatientTokenType.PortalAccess);

        // Assert
        Assert.Null(await service.ValidateTokenAsync(token1.Token, PatientTokenType.PortalAccess));
        Assert.NotNull(await service.ValidateTokenAsync(token2.Token, PatientTokenType.AppointmentConfirmation));
    }

    [Fact]
    public async Task Token_UniqueConstraint_GeneratesDifferentTokens()
    {
        // Arrange
        await using var context = CreateContext(nameof(Token_UniqueConstraint_GeneratesDifferentTokens));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var service = CreateTokenService(context);

        // Act - Crear múltiples tokens
        var tokens = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var token = await service.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);
            tokens.Add(token.Token);
        }

        // Assert - Todos deben ser únicos
        Assert.Equal(10, tokens.Distinct().Count());
    }

    [Fact]
    public async Task CreateAppointmentManagementTokenAsync_CreatesTokenLinkedToAppointment()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAppointmentManagementTokenAsync_CreatesTokenLinkedToAppointment));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        var appointmentId = Guid.NewGuid();
        await context.SaveChangesAsync();

        var service = CreateTokenService(context);

        // Act
        var token = await service.CreateAppointmentManagementTokenAsync(patient.Id, appointmentId, clinic.Id);

        // Assert
        Assert.NotNull(token);
        Assert.Equal(patient.Id, token.PatientId);
        Assert.Equal(appointmentId, token.AppointmentId);
        Assert.Equal(clinic.Id, token.ClinicId);
        Assert.Equal(PatientTokenType.AppointmentManagement, token.Type);
        Assert.True(token.ExpiresAt > DateTime.UtcNow.AddDays(40));

        // Validate without consuming
        var validated = await service.ValidateTokenAsync(token.Token, PatientTokenType.AppointmentManagement);
        Assert.NotNull(validated);
        Assert.Equal(appointmentId, validated!.AppointmentId);

        // Validate allowUsed when used
        var consumed = await service.ValidateAndConsumeTokenAsync(token.Token, PatientTokenType.AppointmentManagement);
        Assert.NotNull(consumed);
        Assert.NotNull(consumed!.UsedAt);

        var normalCheck = await service.ValidateTokenAsync(token.Token, PatientTokenType.AppointmentManagement, allowUsed: false);
        Assert.Null(normalCheck);

        var allowedCheck = await service.ValidateTokenAsync(token.Token, PatientTokenType.AppointmentManagement, allowUsed: true);
        Assert.NotNull(allowedCheck);
    }
}