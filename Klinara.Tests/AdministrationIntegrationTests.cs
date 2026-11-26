using System;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Klinara.Web.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Klinara.Tests;

public class AdministrationIntegrationTests
{
    private sealed class IntegrationTestFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private IntegrationTestFixture(ServiceProvider provider)
        {
            _provider = provider;
            UserManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            Administration = provider.GetRequiredService<IUserAdministrationService>();
            ClinicService = provider.GetRequiredService<IClinicService>();
            ScheduleService = provider.GetRequiredService<IScheduleService>();
            AppointmentService = provider.GetRequiredService<IAppointmentService>();
            DbContext = provider.GetRequiredService<ApplicationDbContext>();
        }

        public UserManager<ApplicationUser> UserManager { get; }
        public RoleManager<IdentityRole<Guid>> RoleManager { get; }
        public IUserAdministrationService Administration { get; }
        public IClinicService ClinicService { get; }
        public IScheduleService ScheduleService { get; }
        public IAppointmentService AppointmentService { get; }
        public ApplicationDbContext DbContext { get; }

        private ApplicationUser? _platformAdmin;

        public static async Task<IntegrationTestFixture> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
                options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
            })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddTokenProvider<EmailTokenProvider<ApplicationUser>>(TokenOptions.DefaultEmailProvider);
            services.AddScoped<IUserAdministrationService, UserAdministrationService>();
            services.AddScoped<IClinicService, ClinicService>();
            services.AddScoped<IScheduleService, ScheduleService>();
            services.AddScoped<IAppointmentService, AppointmentService>();

            var fixture = new IntegrationTestFixture(services.BuildServiceProvider());

            // Seed roles
            foreach (var role in KlinaraRoles.All)
            {
                var roleResult = await fixture.RoleManager.CreateAsync(new IdentityRole<Guid>(role));
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException($"Failed to create role {role}: {string.Join("; ", roleResult.Errors.Select(e => e.Description))}");
            }

            // Create platform admin once
            await fixture.GetOrCreatePlatformAdminAsync();

            return fixture;
        }

        public async Task<ApplicationUser> GetOrCreatePlatformAdminAsync()
        {
            if (_platformAdmin != null) return _platformAdmin;

            var existing = await UserManager.FindByEmailAsync("platform@test.com");
            if (existing != null)
            {
                _platformAdmin = existing;
                return _platformAdmin;
            }

            var user = new ApplicationUser
            {
                UserName = "platform@test.com",
                Email = "platform@test.com",
                EmailConfirmed = true,
                ClinicId = null
            };
            var createResult = await UserManager.CreateAsync(user, "Klinara.Test#123");
            if (!createResult.Succeeded)
                throw new InvalidOperationException($"Failed to create platform admin: {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            var roleResult = await UserManager.AddToRoleAsync(user, KlinaraRoles.PlatformAdmin);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"Failed to add role: {string.Join("; ", roleResult.Errors.Select(e => e.Description))}");

            _platformAdmin = user;
            return _platformAdmin;
        }

        public async Task<Clinic> CreateClinicAsync(string name, string subdomain, string contactEmail = null, string? phone = null, string? address = null, string timeZoneId = "America/Mexico_City")
        {
            var dto = new CreateClinicDto(name, subdomain, contactEmail ?? $"admin@{subdomain}.test", phone, address, timeZoneId);
            var platformAdmin = await GetOrCreatePlatformAdminAsync();
            var result = await ClinicService.CreateAsync(dto, platformAdmin.Id);
            if (!result.Success)
                throw new InvalidOperationException($"Failed to create clinic: {string.Join("; ", result.Errors)}");
            return result.Clinic!;
        }

        public async Task<ApplicationUser> CreateUserAsync(string email, Guid? clinicId, string role)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                ClinicId = clinicId
            };
            var createResult = await UserManager.CreateAsync(user, "Klinara.Test#123");
            if (!createResult.Succeeded)
                throw new InvalidOperationException($"Failed to create user: {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            var roleResult = await UserManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"Failed to add role: {string.Join("; ", roleResult.Errors.Select(e => e.Description))}");
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    // ==========================================
    // FLUJO COMPLETO: PlatformAdmin → ClinicAdmin → Doctor → Patient → Appointment
    // ==========================================

    [Fact]
    public async Task PlatformAdmin_CreatesClinic_CreatesAdminClinica_AssignsDoctor_CreatesPatient_BooksAppointment()
    {
        await using var fixture = await IntegrationTestFixture.CreateAsync();

        // 1. PlatformAdmin crea clínica
        var clinic = await fixture.CreateClinicAsync(
            "Clínica Integración",
            "integracion-test",
            "admin@integracion-test.com",
            "555-0001",
            "Av. Principal 123",
            "America/Mexico_City"
        );
        var clinicId = clinic.Id;

        // 2. PlatformAdmin crea AdminClinica para la clínica
        var adminClinica = await fixture.CreateUserAsync("adminclinica@test.com", clinicId, KlinaraRoles.ClinicAdmin);

        // 3. PlatformAdmin crea Doctor
        var doctor = await fixture.CreateUserAsync("doctor@integracion.com", clinicId, KlinaraRoles.Doctor);
        var doctorId = doctor.Id;

        // 4. Crear Paciente
        var platformAdmin = await fixture.GetOrCreatePlatformAdminAsync();
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            FullName = "María González",
            Email = "paciente@integracion.com",
            Phone = "555-0004",
            DateOfBirth = new DateTime(1990, 5, 15),
            Gender = "F",
            CreatedByUserId = platformAdmin.Id,
            CreatedAt = DateTime.UtcNow
        };
        fixture.DbContext.Patients.Add(patient);
        await fixture.DbContext.SaveChangesAsync();
        var patientId = patient.Id;

        // 5. Doctor crea horario (Lunes 9-14)
        var schedule = new DoctorSchedule
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            DoctorId = doctorId,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(14, 0),
            SlotDurationMinutes = 30,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.Date.AddDays(-30)
        };
        fixture.DbContext.DoctorSchedules.Add(schedule);
        await fixture.DbContext.SaveChangesAsync();

        // 6. Generar slots para la próxima semana
        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        // 7. Agendar cita
        var slots = await fixture.ScheduleService.GenerateSlotsForPeriodAsync(clinicId, doctorId, nextMonday.Date, nextMonday.Date.AddDays(7));
        Assert.NotEmpty(slots);

        // Create appointment using same approach as ScheduleServiceTests - local DateTime
        // Use Unspecified kind to indicate clinic local time (not UTC)
        var appointmentStart = DateTime.SpecifyKind(nextMonday.Date.AddHours(10), DateTimeKind.Unspecified); // 10:00 AM local
        var appointmentEnd = DateTime.SpecifyKind(appointmentStart.AddMinutes(30), DateTimeKind.Unspecified);

        // Get doctor schedules for debug
        var doctorSchedules = await fixture.ScheduleService.GetDoctorSchedulesAsync(clinicId, doctorId, appointmentStart);
        var scheduleInfo = string.Join("; ", doctorSchedules.Select(ds => $"Day={ds.DayOfWeek}, {ds.StartTime}-{ds.EndTime}"));

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            DoctorId = doctorId,
            PatientId = patientId,
            StartAt = appointmentStart,
            EndAt = appointmentEnd,
            Type = AppointmentType.FirstVisit,
            Notes = "Primera consulta",
            CreatedByUserId = platformAdmin.Id,
            CreatedAt = DateTime.UtcNow
        };

        var appointmentResult = await fixture.AppointmentService.CreateAppointmentAsync(appointment, isPatientBooking: true);
        var errors = string.Join(", ", appointmentResult.Errors);

        if (!appointmentResult.Success)
        {
            var debugInfo = $" | Appt: {appointmentStart} ({appointmentStart.Kind}) -> Expected Local: {nextMonday.Date.AddHours(10):HH:mm} | Schedules: {scheduleInfo}";
            throw new InvalidOperationException($"Appointment creation failed: {errors}{debugInfo}");
        }

        var createdAppointment = appointmentResult.Appointment!;

        // Verificar que el slot se marcó como reservado
        var bookedSlot = await fixture.DbContext.TimeSlots.FirstOrDefaultAsync(s => s.AppointmentId == createdAppointment.Id);
        Assert.NotNull(bookedSlot);
        Assert.True(bookedSlot.IsBooked);

        // Verificar que la cita aparece en listas
        var clinicAppointments = await fixture.AppointmentService.GetAppointmentsByClinicAsync(clinicId, nextMonday.Date, nextMonday.Date.AddDays(7));
        Assert.Contains(clinicAppointments, a => a.Id == createdAppointment.Id);
    }

    // ==========================================
    // TESTS DE AISLAMIENTO DE CLÍNICAS
    // ==========================================

    [Fact]
    public async Task ClinicAdmin_CannotCreateInOtherClinic()
    {
        await using var fixture = await IntegrationTestFixture.CreateAsync();

        // Create Clinic 1 with AdminClinica
        var clinic1 = await fixture.CreateClinicAsync("Clínica 1", "clinica-1", "admin1@test.com", "555-1111", "Dir 1");
        var clinic1Id = clinic1.Id;

        // Create Clinic 2
        var clinic2 = await fixture.CreateClinicAsync("Clínica 2", "clinica-2", "admin2@test.com", "555-2222", "Dir 2");
        var clinic2Id = clinic2.Id;

        // Create AdminClinica for Clinic 1
        var admin1 = await fixture.CreateUserAsync("admin1@test.com", clinic1Id, KlinaraRoles.ClinicAdmin);
        var admin1Id = admin1.Id;

        // Act - Admin de clínica 1 intenta crear doctor en clínica 2
        var dtoOther = new CreateUserDto("doctor2@test.com", "Dr. Dos", KlinaraRoles.Doctor, clinic2Id, "555-2222");
        var resultOther = await fixture.Administration.CreateAsync(dtoOther, admin1Id);

        // Assert - Debe fallar porque ClinicAdmin solo puede crear en su propia clínica
        Assert.False(resultOther.Success);
        Assert.Contains("clínica", resultOther.Errors[0].ToLower());
    }

    [Fact]
    public async Task ClinicAdmin_CanCreateInOwnClinic()
    {
        await using var fixture = await IntegrationTestFixture.CreateAsync();

        // Create Clinic with AdminClinica
        var clinic = await fixture.CreateClinicAsync("Clínica 1", "clinica-1", "admin1@test.com", "555-1111", "Dir 1");
        var clinicId = clinic.Id;

        // Create AdminClinica for Clinic
        var admin = await fixture.CreateUserAsync("admin1@test.com", clinicId, KlinaraRoles.ClinicAdmin);
        var adminId = admin.Id;

        // Act - Admin de clínica 1 crea doctor en su clínica
        var dtoOwn = new CreateUserDto("doctor1@test.com", "Dr. Uno", KlinaraRoles.Doctor, clinicId, "555-1111");
        var resultOwn = await fixture.Administration.CreateAsync(dtoOwn, adminId);

        // Assert - Debe permitir crear
        Assert.True(resultOwn.Success);
        Assert.NotNull(resultOwn.User);
        Assert.Equal(clinicId, resultOwn.User!.ClinicId);
    }

    // ==========================================
    // VALIDACIONES DE UNICIDAD
    // ==========================================

    [Fact]
    public async Task Subdomain_Unique_Validation()
    {
        await using var fixture = await IntegrationTestFixture.CreateAsync();

        var dto = new CreateClinicDto("Clínica A", "mismo-sub", "a@a.com", "111", "Dir 1", "America/Mexico_City");
        var platformAdmin = await fixture.GetOrCreatePlatformAdminAsync();
        var result1 = await fixture.ClinicService.CreateAsync(dto, platformAdmin.Id);
        Assert.True(result1.Success);

        var dto2 = new CreateClinicDto("Clínica B", "mismo-sub", "b@b.com", "222", "Dir 2", "America/Mexico_City");
        var result2 = await fixture.ClinicService.CreateAsync(dto2, platformAdmin.Id);

        Assert.False(result2.Success);
        Assert.Contains("subdominio", result2.Errors[0].ToLower());
    }

    [Fact]
    public async Task Email_Unique_Validation()
    {
        await using var fixture = await IntegrationTestFixture.CreateAsync();

        var platformAdmin = await fixture.GetOrCreatePlatformAdminAsync();

        // Create Clinic
        var clinic = await fixture.CreateClinicAsync("Test", "test", "admin@test.com", "555-0000", "Dirección");
        var clinicId = clinic.Id;

        // Create first user
        var dto1 = new CreateUserDto("dup@test.com", "User 1", KlinaraRoles.Doctor, clinicId, "111");
        var result1 = await fixture.Administration.CreateAsync(dto1, platformAdmin.Id);
        Assert.True(result1.Success);

        // Try to create second user with same email
        var dto2 = new CreateUserDto("dup@test.com", "User 2", KlinaraRoles.Employee, clinicId, "222");
        var result2 = await fixture.Administration.CreateAsync(dto2, platformAdmin.Id);

        Assert.False(result2.Success);
        Assert.Contains("correo", result2.Errors[0].ToLower());
    }

    // ==========================================
    // SECURITY STAMP ROTATION
    // ==========================================

    [Fact]
    public async Task ChangeRole_RotatesSecurityStamp()
    {
        await using var fixture = await IntegrationTestFixture.CreateAsync();

        var platformAdmin = await fixture.GetOrCreatePlatformAdminAsync();

        // Create Clinic
        var clinic = await fixture.CreateClinicAsync("Test", "test", "admin@test.com", "555-0000", "Dirección");
        var clinicId = clinic.Id;

        // Create user
        var userDto = new CreateUserDto("user@test.com", "Test User", KlinaraRoles.Doctor, clinicId, "Klinara.Test#123");
        var userResult = await fixture.Administration.CreateAsync(userDto, platformAdmin.Id);
        var userCreateErrors = string.Join(", ", userResult.Errors);
        Assert.True(userResult.Success, $"User creation failed: {userCreateErrors}");
        var userId = userResult.User!.Id;

        var originalSecurityStamp = userResult.User.SecurityStamp;

        // Change role
        var changeResult = await fixture.Administration.ChangeRoleAsync(userId, KlinaraRoles.Employee, platformAdmin.Id);
        var changeErrors = string.Join(", ", changeResult.Errors);
        Assert.True(changeResult.Success, $"ChangeRole failed: {changeErrors}");

        // Verify security stamp rotated
        var updatedUser = await fixture.UserManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(updatedUser);
        Assert.NotEqual(originalSecurityStamp, updatedUser!.SecurityStamp);
    }

    [Fact]
    public async Task ChangeClinic_RotatesSecurityStamp()
    {
        await using var fixture = await IntegrationTestFixture.CreateAsync();

        var platformAdmin = await fixture.GetOrCreatePlatformAdminAsync();

        // Create two clinics
        var clinic1 = await fixture.CreateClinicAsync("Clínica 1", "clinica-1", "admin1@test.com", "555-1111", "Dir 1");
        var clinic1Id = clinic1.Id;

        var clinic2 = await fixture.CreateClinicAsync("Clínica 2", "clinica-2", "admin2@test.com", "555-2222", "Dir 2");
        var clinic2Id = clinic2.Id;

        // Create user in clinic 1
        var userDto = new CreateUserDto("user@test.com", "Test User", KlinaraRoles.Doctor, clinic1Id, "555-1111");
        var userResult = await fixture.Administration.CreateAsync(userDto, platformAdmin.Id);
        var userCreateErrors = string.Join(", ", userResult.Errors);
        Assert.True(userResult.Success, $"User creation failed: {userCreateErrors}");
        var userId = userResult.User!.Id;

        var originalSecurityStamp = userResult.User.SecurityStamp;

        // Change clinic
        var changeResult = await fixture.Administration.ChangeClinicAsync(userId, clinic2Id, platformAdmin.Id);
        var changeErrors = string.Join(", ", changeResult.Errors);
        Assert.True(changeResult.Success, $"ChangeClinic failed: {changeErrors}");

        // Verify security stamp rotated
        var updatedUser = await fixture.UserManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(updatedUser);
        Assert.NotEqual(originalSecurityStamp, updatedUser!.SecurityStamp);
        Assert.Equal(clinic2Id, updatedUser!.ClinicId);
    }

    [Fact]
    public async Task ResetPassword_RotatesSecurityStamp()
    {
        await using var fixture = await IntegrationTestFixture.CreateAsync();

        var platformAdmin = await fixture.GetOrCreatePlatformAdminAsync();

        // Create Clinic
        var clinic = await fixture.CreateClinicAsync("Test", "test", "admin@test.com", "555-0000", "Dirección");
        var clinicId = clinic.Id;

        // Create user
        var userDto = new CreateUserDto("user@test.com", "Test User", KlinaraRoles.Doctor, clinicId, "Klinara.Test#123");
        var userResult = await fixture.Administration.CreateAsync(userDto, platformAdmin.Id);
        var userCreateErrors = string.Join(", ", userResult.Errors);
        Assert.True(userResult.Success, $"User creation failed: {userCreateErrors}");
        var userId = userResult.User!.Id;

        var originalSecurityStamp = userResult.User.SecurityStamp;

        // Reset password
        var resetResult = await fixture.Administration.ResetPasswordAsync(userId, platformAdmin.Id);
        var resetErrors = string.Join(", ", resetResult.Errors);
        Assert.True(resetResult.Success, $"ResetPassword failed: {resetErrors}");

        // Verify security stamp rotated
        var updatedUser = await fixture.UserManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(updatedUser);
        Assert.NotEqual(originalSecurityStamp, updatedUser!.SecurityStamp);
    }
}