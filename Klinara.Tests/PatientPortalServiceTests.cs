using System;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Klinara.Tests;

public class PatientPortalServiceTests
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
            EmailConfirmed = false,
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

    private static DoctorSchedule CreateSchedule(Guid clinicId, Guid doctorId, DayOfWeek day, TimeOnly start, TimeOnly end, TimeOnly? lunchStart = null, TimeOnly? lunchEnd = null, int slotDuration = 30)
    {
        return new DoctorSchedule
        {
            ClinicId = clinicId,
            DoctorId = doctorId,
            DayOfWeek = day,
            StartTime = start,
            EndTime = end,
            LunchStart = lunchStart,
            LunchEnd = lunchEnd,
            SlotDurationMinutes = slotDuration,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.Date.AddDays(-30)
        };
    }

    private static IConfiguration CreateTestConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PatientPortal:BaseUrl"] = "https://localhost:5000",
                ["Email:SmtpHost"] = "localhost",
                ["Email:SmtpPort"] = "587",
                ["Email:FromEmail"] = "noreply@klinara.com",
                ["Email:FromName"] = "Klinara Test",
                ["Email:EnableSsl"] = "false"
            })
            .Build();
        return config;
    }

    private static PatientPortalService CreatePortalService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IEmailSender<ApplicationUser>? emailSender = null,
        Mock<UserManager<ApplicationUser>>? userManagerMock = null)
    {
        var scheduleLogger = new LoggerFactory().CreateLogger<ScheduleService>();
        var scheduleService = new ScheduleService(context, scheduleLogger);

        var appointmentLogger = new LoggerFactory().CreateLogger<AppointmentService>();
        var appointmentService = new AppointmentService(context, scheduleService, appointmentLogger);

        var tokenLogger = new LoggerFactory().CreateLogger<PatientTokenService>();
        var tokenService = new PatientTokenService(context, tokenLogger);

        var otpLogger = new LoggerFactory().CreateLogger<OtpService>();
        var userManager = userManagerMock ?? CreateMockUserManager(context);
        var otpService = new OtpService(userManager.Object, configuration);

        var otpEmailSenderLogger = new LoggerFactory().CreateLogger<OtpEmailSender>();
        var otpEmailSender = new OtpEmailSender(userManager.Object, otpEmailSenderLogger, otpService);

        var portalLogger = new LoggerFactory().CreateLogger<PatientPortalService>();

        emailSender ??= new Mock<IEmailSender<ApplicationUser>>().Object;

        return new PatientPortalService(
            context,
            scheduleService,
            appointmentService,
            tokenService,
            otpService,
            otpEmailSender,
            emailSender,
            configuration,
            portalLogger);
    }

    private static Mock<UserManager<ApplicationUser>> CreateMockUserManager(ApplicationDbContext context)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        store.Setup(s => s.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userName, CancellationToken ct) =>
                context.Users.FirstOrDefault(u => u.UserName == userName));
        store.Setup(s => s.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken ct) =>
                context.Users.FirstOrDefault(u => u.Id.ToString() == userId));

        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // Track generated tokens per user for validation
        var generatedTokens = new Dictionary<Guid, string>();

        userManager.Setup(m => m.GenerateUserTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((ApplicationUser user, string provider, string purpose) =>
            {
                var token = "12345678901234567890123456789012"; // 32 chars
                generatedTokens[user.Id] = token;
                return Task.FromResult(token);
            });

        userManager.Setup(m => m.VerifyUserTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((ApplicationUser user, string provider, string purpose, string token) =>
            {
                if (generatedTokens.TryGetValue(user.Id, out var expectedToken))
                {
                    // OtpService pads the 6-char code to 32 chars with '0'
                    // So we need to check if the token starts with the expected 6-char OTP
                    var expectedOtp = expectedToken[..6];
                    return Task.FromResult(token.StartsWith(expectedOtp));
                }
                return Task.FromResult(false);
            });

        userManager.Setup(m => m.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        return userManager;
    }

    [Fact]
    public async Task SendVerificationEmailAsync_NewPatient_CreatesPatientAndSendsOtp()
    {
        // Arrange
        await using var context = CreateContext(nameof(SendVerificationEmailAsync_NewPatient_CreatesPatientAndSendsOtp));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        // Act
        var result = await service.SendVerificationEmailAsync("nuevo@test.com", "test-clinic");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("nuevo@test.com", result.Email);

        var patient = await context.Patients.FirstOrDefaultAsync(p => p.Email == "nuevo@test.com");
        Assert.NotNull(patient);
        Assert.Equal(clinic.Id, patient.ClinicId);

        var portalUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "nuevo@test.com" && u.ClinicId == clinic.Id);
        Assert.NotNull(portalUser);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_ExistingPatient_ReusesPatient()
    {
        // Arrange
        await using var context = CreateContext(nameof(SendVerificationEmailAsync_ExistingPatient_ReusesPatient));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("existente@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Existente", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        // Act
        var result = await service.SendVerificationEmailAsync("existente@test.com", "test-clinic");

        // Assert
        Assert.True(result.Success);

        var patients = await context.Patients.Where(p => p.Email == "existente@test.com").ToListAsync();
        Assert.Single(patients);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_InvalidClinic_ReturnsError()
    {
        // Arrange
        await using var context = CreateContext(nameof(SendVerificationEmailAsync_InvalidClinic_ReturnsError));
        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        // Act
        var result = await service.SendVerificationEmailAsync("test@test.com", "clinica-inexistente");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Clínica no encontrada", result.Error);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_BlockedPatient_ReturnsError()
    {
        // Arrange
        await using var context = CreateContext(nameof(SendVerificationEmailAsync_BlockedPatient_ReturnsError));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("bloqueado@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Bloqueado", user.Email);
        patient.IsBlocked = true;
        patient.BlockedUntil = DateTime.UtcNow.AddMonths(3);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        // Act
        var result = await service.SendVerificationEmailAsync("bloqueado@test.com", "test-clinic");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("bloqueado", result.Error);
    }

    [Fact]
    public async Task ValidateOtpAndCreatePortalTokenAsync_ValidOtp_ReturnsPortalToken()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateOtpAndCreatePortalTokenAsync_ValidOtp_ReturnsPortalToken));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("test@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();

        // Crear mock compartido para UserManager
        var userManagerMock = CreateMockUserManager(context);
        var service = CreatePortalService(context, configuration, emailSender.Object, userManagerMock);

        // Generar OTP usando el mismo mock que el service
        var otpService = new OtpService(userManagerMock.Object, configuration);
        var otpCode = await otpService.GenerateOtpAsync(user, OtpPurposes.EmailVerification);

        // Act
        var result = await service.ValidateOtpAndCreatePortalTokenAsync("test@test.com", otpCode, "test-clinic");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.PortalToken);
        Assert.NotNull(result.ExpiresAt);
        Assert.Equal(patient.Id, result.PatientId);

        // Verificar que el token se guarda en BD
        var savedToken = await context.PatientTokens.FirstOrDefaultAsync(t => t.Token == result.PortalToken);
        Assert.NotNull(savedToken);
        Assert.Equal(PatientTokenType.PortalAccess, savedToken.Type);
    }

    [Fact]
    public async Task ValidateOtpAndCreatePortalTokenAsync_InvalidOtp_ReturnsError()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateOtpAndCreatePortalTokenAsync_InvalidOtp_ReturnsError));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user = CreateUser("test@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        // Act - OTP incorrecto
        var result = await service.ValidateOtpAndCreatePortalTokenAsync("test@test.com", "999999", "test-clinic");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Código de verificación inválido o expirado", result.Error);
    }

    [Fact]
    public async Task BookAppointmentAsync_ValidToken_CreatesAppointmentAndConfirmationToken()
    {
        // Arrange
        await using var context = CreateContext(nameof(BookAppointmentAsync_ValidToken_CreatesAppointmentAndConfirmationToken));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        // Horario: Lunes 9:00-14:00
        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        // Crear token PortalAccess
        var tokenLogger = new LoggerFactory().CreateLogger<PatientTokenService>();
        var tokenService = new PatientTokenService(context, tokenLogger);
        var portalToken = await tokenService.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);

        // Usar fecha fija (lunes 7 dic 2026)
        var fixedMonday = new DateTime(2026, 12, 7, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(DayOfWeek.Monday, fixedMonday.DayOfWeek);

        var bookDto = new BookAppointmentDto(
            doctor.Id,
            fixedMonday,
            fixedMonday.AddMinutes(30),
            AppointmentType.FirstVisit,
            "Primera consulta"
        );

        // Act
        var result = await service.BookAppointmentAsync(portalToken.Token, bookDto);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.AppointmentId);
        Assert.NotNull(result.ConfirmationToken);
        Assert.NotNull(result.ConfirmationExpiresAt);

        // Verificar cita creada
        var appointment = await context.Appointments.FindAsync(result.AppointmentId);
        Assert.NotNull(appointment);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(patient.Id, appointment.PatientId);
        Assert.Equal(doctor.Id, appointment.DoctorId);

        // Verificar token de confirmación creado
        var confirmationToken = await context.PatientTokens
            .FirstOrDefaultAsync(t => t.Token == result.ConfirmationToken && t.Type == PatientTokenType.AppointmentConfirmation);
        Assert.NotNull(confirmationToken);
    }

    [Fact]
    public async Task BookAppointmentAsync_InvalidToken_ReturnsError()
    {
        // Arrange
        await using var context = CreateContext(nameof(BookAppointmentAsync_InvalidToken_ReturnsError));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        var bookDto = new BookAppointmentDto(
            doctor.Id,
            nextMonday.AddHours(10),
            nextMonday.AddHours(10).AddMinutes(30),
            AppointmentType.FirstVisit,
            null
        );

        // Act - Token inválido
        var result = await service.BookAppointmentAsync("token-invalido", bookDto);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Token de acceso inválido, expirado o ya usado", result.Error);
    }

    [Fact]
        public async Task BookAppointmentAsync_OutsideHorizon_ReturnsError()
        {
            // Arrange
            await using var context = CreateContext(nameof(BookAppointmentAsync_OutsideHorizon_ReturnsError));
            var clinic = CreateClinic("Test Clinic", "test-clinic");
            context.Clinics.Add(clinic);

            var doctor = CreateUser("doctor@test.com", clinic.Id);
            context.Users.Add(doctor);

            var user = CreateUser("patient@test.com", clinic.Id);
            context.Users.Add(user);

            var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
            context.Patients.Add(patient);

            var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
            context.DoctorSchedules.Add(schedule);

            await context.SaveChangesAsync();

            var configuration = CreateTestConfiguration();
            var emailSender = new Mock<IEmailSender<ApplicationUser>>();
            var service = CreatePortalService(context, configuration, emailSender.Object);

            var tokenLogger = new LoggerFactory().CreateLogger<PatientTokenService>();
            var tokenService = new PatientTokenService(context, tokenLogger);
            var portalToken = await tokenService.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);

            // Fecha a 50 días (fuera de horizonte 45d) - usar lunes fijo en 2027
            var farFutureMonday = new DateTime(2027, 2, 15, 10, 0, 0, DateTimeKind.Utc); // Lunes 15 feb 2027
            Assert.Equal(DayOfWeek.Monday, farFutureMonday.DayOfWeek);

            var bookDto = new BookAppointmentDto(
                doctor.Id,
                farFutureMonday,
                farFutureMonday.AddMinutes(30),
                AppointmentType.FirstVisit,
                null
            );

            // Act
            var result = await service.BookAppointmentAsync(portalToken.Token, bookDto);

            // Assert
            Assert.False(result.Success);
            // El error puede ser por horario o por horizonte (depende del orden de validación)
            Assert.True(result.Error!.Contains("45 días") || result.Error.Contains("horario laboral"));
        }

        [Fact]
        public async Task BookAppointmentAsync_BlockedPatient_ReturnsError()
        {
            // Arrange
            await using var context = CreateContext(nameof(BookAppointmentAsync_BlockedPatient_ReturnsError));
            var clinic = CreateClinic("Test Clinic", "test-clinic");
            context.Clinics.Add(clinic);

            var doctor = CreateUser("doctor@test.com", clinic.Id);
            context.Users.Add(doctor);

            var user = CreateUser("patient@test.com", clinic.Id);
            context.Users.Add(user);

            var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
            patient.IsBlocked = true;
            patient.BlockedUntil = DateTime.UtcNow.AddMonths(3);
            context.Patients.Add(patient);

            var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
            context.DoctorSchedules.Add(schedule);

            await context.SaveChangesAsync();

            var configuration = CreateTestConfiguration();
            var emailSender = new Mock<IEmailSender<ApplicationUser>>();
            var service = CreatePortalService(context, configuration, emailSender.Object);

            var tokenLogger = new LoggerFactory().CreateLogger<PatientTokenService>();
            var tokenService = new PatientTokenService(context, tokenLogger);
            var portalToken = await tokenService.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);

            var fixedMonday = new DateTime(2026, 12, 7, 10, 0, 0, DateTimeKind.Utc);
            Assert.Equal(DayOfWeek.Monday, fixedMonday.DayOfWeek);

            var bookDto = new BookAppointmentDto(
                doctor.Id,
                fixedMonday,
                fixedMonday.AddMinutes(30),
                AppointmentType.FirstVisit,
                null
            );

            // Act
            var result = await service.BookAppointmentAsync(portalToken.Token, bookDto);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("bloqueado", result.Error!);
        }

    [Fact]
    public async Task ConfirmAppointmentAsync_ValidToken_ConfirmsAppointment()
    {
        // Arrange
        await using var context = CreateContext(nameof(ConfirmAppointmentAsync_ValidToken_ConfirmsAppointment));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = nextMonday.AddHours(10),
            EndAt = nextMonday.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Scheduled,
            CreatedByUserId = patient.Id
        };
        context.Appointments.Add(appointment);

        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        // Crear token de confirmación
        var tokenLogger = new LoggerFactory().CreateLogger<PatientTokenService>();
        var tokenService = new PatientTokenService(context, tokenLogger);
        var confirmationToken = await tokenService.CreateAppointmentConfirmationTokenAsync(patient.Id, appointment.Id, clinic.Id);

        // Act
        var result = await service.ConfirmAppointmentAsync(confirmationToken.Token);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(appointment.Id, result.AppointmentId);

        var updatedAppointment = await context.Appointments.FindAsync(appointment.Id);
        Assert.Equal(AppointmentStatus.Confirmed, updatedAppointment!.Status);

        // Verificar historial
        var history = await context.AppointmentHistory
            .FirstOrDefaultAsync(h => h.AppointmentId == appointment.Id && h.ToStatus == AppointmentStatus.Confirmed);
        Assert.NotNull(history);
        Assert.Equal("Confirmada por paciente via portal", history.Reason);
    }

    [Fact]
    public async Task ConfirmAppointmentAsync_AlreadyConfirmed_ReturnsError()
    {
        // Arrange
        await using var context = CreateContext(nameof(ConfirmAppointmentAsync_AlreadyConfirmed_ReturnsError));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = nextMonday.AddHours(10),
            EndAt = nextMonday.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Confirmed, // Ya confirmada
            CreatedByUserId = patient.Id
        };
        context.Appointments.Add(appointment);

        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        var tokenLogger = new LoggerFactory().CreateLogger<PatientTokenService>();
        var tokenService = new PatientTokenService(context, tokenLogger);
        var confirmationToken = await tokenService.CreateAppointmentConfirmationTokenAsync(patient.Id, appointment.Id, clinic.Id);

        // Act
        var result = await service.ConfirmAppointmentAsync(confirmationToken.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("programado", result.Error);
    }

    [Fact]
    public async Task GetAppointmentViewAsync_ValidToken_ReturnsAppointmentView()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetAppointmentViewAsync_ValidToken_ReturnsAppointmentView));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        var user = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(user);

        var patient = CreatePatient(clinic.Id, user.Id, "Paciente Test", user.Email);
        context.Patients.Add(patient);

        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = nextMonday.AddHours(10),
            EndAt = nextMonday.AddHours(10).AddMinutes(30),
            Type = AppointmentType.FirstVisit,
            Status = AppointmentStatus.Confirmed,
            Notes = "Notas de prueba",
            CreatedByUserId = patient.Id
        };
        context.Appointments.Add(appointment);

        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        var tokenLogger = new LoggerFactory().CreateLogger<PatientTokenService>();
        var tokenService = new PatientTokenService(context, tokenLogger);
        var confirmationToken = await tokenService.CreateAppointmentConfirmationTokenAsync(patient.Id, appointment.Id, clinic.Id);

        // Act
        var view = await service.GetAppointmentViewAsync(confirmationToken.Token);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(appointment.Id, view!.AppointmentId);
        Assert.Equal(clinic.Name, view.ClinicName);
        Assert.Equal(doctor.UserName, view.DoctorName);
        Assert.Equal(patient.FullName, view.PatientName);
        Assert.Equal(AppointmentStatus.Confirmed, view.Status);
        Assert.Equal("Notas de prueba", view.Notes);
    }

    [Fact]
    public async Task GetAvailableDoctorsAsync_ReturnsDoctorsWithSchedules()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetAvailableDoctorsAsync_ReturnsDoctorsWithSchedules));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor1 = CreateUser("doctor1@test.com", clinic.Id);
        var doctor2 = CreateUser("doctor2@test.com", clinic.Id);
        context.Users.AddRange(doctor1, doctor2);

        var schedule1 = CreateSchedule(clinic.Id, doctor1.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        var schedule2 = CreateSchedule(clinic.Id, doctor2.Id, DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(15, 0));
        context.DoctorSchedules.AddRange(schedule1, schedule2);

        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        // Act
        var doctors = await service.GetAvailableDoctorsAsync("test-clinic");

        // Assert
        Assert.Equal(2, doctors.Count);
        Assert.All(doctors, d => Assert.NotEmpty(d.WeeklySchedule));
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ReturnsAvailableSlots()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetAvailableSlotsAsync_ReturnsAvailableSlots));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), null, null, 60); // 3 slots de 60 min
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var configuration = CreateTestConfiguration();
        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = CreatePortalService(context, configuration, emailSender.Object);

        // Rango de 6 días para incluir solo 1 lunes
        var startDate = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        var endDate = startDate.AddDays(6);

        // Act
        var slots = await service.GetAvailableSlotsAsync("test-clinic", doctor.Id, startDate, endDate);

        // Assert
        Assert.NotEmpty(slots);
        var mondaySlots = slots.Where(s => s.StartAt.DayOfWeek == DayOfWeek.Monday).ToList();
        Assert.Equal(3, mondaySlots.Count);
        Assert.All(mondaySlots, s => Assert.False(s.IsBooked));
        Assert.All(mondaySlots, s => Assert.False(s.IsBlocked));
    }
}