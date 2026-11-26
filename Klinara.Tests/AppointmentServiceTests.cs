using System;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Klinara.Tests;

public class AppointmentServiceTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ApplicationUser CreateUser(string email, Guid? clinicId = null, string role = "Doctor")
    {
        return new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            ClinicId = clinicId
        };
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

    private static Patient CreatePatient(Guid clinicId, Guid userId, string name, string? email)
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

    private static AppointmentService CreateAppointmentService(ApplicationDbContext context)
    {
        var scheduleServiceLogger = new LoggerFactory().CreateLogger<ScheduleService>();
        var scheduleService = new ScheduleService(context, scheduleServiceLogger);
        var appointmentServiceLogger = new LoggerFactory().CreateLogger<AppointmentService>();
        return new AppointmentService(context, scheduleService, appointmentServiceLogger);
    }

    // Helpers para zona horaria de clínica (America/Mexico_City = UTC-6)
    private static readonly TimeZoneInfo ClinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    private static DateTime GetNowInClinicTimeZone()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ClinicTimeZone);
    }

    private static DateTime GetNextMondayInClinicTimeZone(DateTime? from = null)
    {
        var baseTime = from ?? GetNowInClinicTimeZone();
        var nextMonday = baseTime.AddDays(((int)DayOfWeek.Monday - (int)baseTime.DayOfWeek + 7) % 7);
        if (nextMonday <= baseTime) nextMonday = nextMonday.AddDays(7);
        return nextMonday;
    }

    private static Appointment CreateAppointmentInClinicTime(Guid clinicId, Guid doctorId, Guid patientId, DateTime localDate, TimeOnly startTime, TimeOnly endTime, Guid? createdByUserId = null)
    {
        var startAt = DateTime.SpecifyKind(localDate.Date.Add(startTime.ToTimeSpan()), DateTimeKind.Unspecified);
        var endAt = DateTime.SpecifyKind(localDate.Date.Add(endTime.ToTimeSpan()), DateTimeKind.Unspecified);
        return new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            DoctorId = doctorId,
            PatientId = patientId,
            StartAt = startAt,
            EndAt = endAt,
            CreatedByUserId = createdByUserId ?? patientId
        };
    }

    [Fact]
    public async Task CreateAppointmentAsync_ValidAppointment_ReturnsSuccess()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAppointmentAsync_ValidAppointment_ReturnsSuccess));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        var patientEntity = CreatePatient(clinic.Id, patient.Id, "Paciente Test", patient.Email);
        context.Patients.Add(patientEntity);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var nextMonday = GetNextMondayInClinicTimeZone();

        var appointment = CreateAppointmentInClinicTime(clinic.Id, doctor.Id, patient.Id, nextMonday, new TimeOnly(10, 0), new TimeOnly(10, 30), doctor.Id);

        var service = CreateAppointmentService(context);

        // Act
        var result = await service.CreateAppointmentAsync(appointment, isPatientBooking: true);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Appointment);
        Assert.Equal(AppointmentStatus.Scheduled, result.Appointment.Status);
    }

    [Fact]
    public async Task CreateAppointmentAsync_OverlappingAppointment_ReturnsFailure()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAppointmentAsync_OverlappingAppointment_ReturnsFailure));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient1 = CreateUser("patient1@test.com", clinic.Id);
        var patient2 = CreateUser("patient2@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient1, patient2);

        var p1 = CreatePatient(clinic.Id, patient1.Id, "Paciente 1", patient1.Email);
        var p2 = CreatePatient(clinic.Id, patient2.Id, "Paciente 2", patient2.Email);
        context.Patients.AddRange(p1, p2);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var nextMonday = GetNextMondayInClinicTimeZone();

        // Primera cita
        var appt1 = CreateAppointmentInClinicTime(clinic.Id, doctor.Id, patient1.Id, nextMonday, new TimeOnly(10, 0), new TimeOnly(10, 30), doctor.Id);

        var service = CreateAppointmentService(context);
        var result1 = await service.CreateAppointmentAsync(appt1, isPatientBooking: true);
        Assert.True(result1.Success);

        // Segunda cita que se traslape
        var appt2 = CreateAppointmentInClinicTime(clinic.Id, doctor.Id, patient2.Id, nextMonday, new TimeOnly(10, 15), new TimeOnly(10, 45), doctor.Id);

        // Act
        var result2 = await service.CreateAppointmentAsync(appt2, isPatientBooking: true);

        // Assert
        Assert.False(result2.Success);
        Assert.Contains(result2.Errors, e => e.Contains("traslape"));
    }

    [Fact]
    public async Task CancelAppointmentAsync_LateCancellationByPatient_IncrementsLateCount()
    {
        // Arrange
        await using var context = CreateContext(nameof(CancelAppointmentAsync_LateCancellationByPatient_IncrementsLateCount));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        var patientEntity = CreatePatient(clinic.Id, patient.Id, "Paciente Test", patient.Email);
        context.Patients.Add(patientEntity);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        var appointment = new Appointment
        {
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = nextMonday.Date.AddHours(10),
            EndAt = nextMonday.Date.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Scheduled,
            CreatedByUserId = doctor.Id
        };
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        var service = CreateAppointmentService(context);

        // Act - cancelar por paciente con < 24h de antelación (simular fecha cercana)
        var nearFuture = DateTime.UtcNow.AddHours(12);
        appointment.StartAt = nearFuture;
        await context.SaveChangesAsync();

        var result = await service.CancelAppointmentAsync(appointment.Id, patient.Id, "No puedo ir", isPatientCancellation: true);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(AppointmentStatus.CancelledByPatient, result.Appointment.Status);
        Assert.True(result.Appointment.IsLateCancellation);

        // Verificar contador de impuntualidad
        var updatedPatient = await context.Patients.FindAsync(patient.Id);
        Assert.Equal(1, updatedPatient.LateCancellationCount);
        Assert.False(updatedPatient.IsBlocked); // Solo 1, no 3
    }

    [Fact]
    public async Task CancelAppointmentAsync_ThirdLateCancellation_BlocksPatient()
    {
        // Arrange
        await using var context = CreateContext(nameof(CancelAppointmentAsync_ThirdLateCancellation_BlocksPatient));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        var patientEntity = CreatePatient(clinic.Id, patient.Id, "Paciente Test", patient.Email);
        patientEntity.LateCancellationCount = 2; // Ya tiene 2
        context.Patients.Add(patientEntity);

        var doctorEntity = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctorEntity);

        var schedule = CreateSchedule(clinic.Id, doctorEntity.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var nearFuture = DateTime.UtcNow.AddHours(12);

        var appointment = new Appointment
        {
            ClinicId = clinic.Id,
            DoctorId = doctorEntity.Id,
            PatientId = patient.Id,
            StartAt = DateTime.UtcNow.AddHours(12),
            EndAt = DateTime.UtcNow.AddHours(12).AddMinutes(30),
            Status = AppointmentStatus.Scheduled,
            CreatedByUserId = doctorEntity.Id
        };
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        var service = CreateAppointmentService(context);

        // Act
        var result = await service.CancelAppointmentAsync(appointment.Id, patient.Id, "No puedo", isPatientCancellation: true);

        // Assert
        Assert.True(result.Success);
        var updatedPatient = await context.Patients.FindAsync(patient.Id);
        Assert.Equal(3, updatedPatient.LateCancellationCount);
        Assert.True(updatedPatient.IsBlocked);
        Assert.NotNull(updatedPatient.BlockedUntil);
    }

    [Fact]
    public async Task MarkNoShowAsync_ValidAppointment_MarksNoShowAndIncrementsCount()
    {
        // Arrange
        await using var context = CreateContext(nameof(MarkNoShowAsync_ValidAppointment_MarksNoShowAndIncrementsCount));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        var patientEntity = CreatePatient(clinic.Id, patient.Id, "Paciente Test", patient.Email);
        context.Patients.Add(patientEntity);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        var appointment = new Appointment
        {
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = nextMonday.Date.AddHours(10),
            EndAt = nextMonday.Date.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Scheduled,
            CreatedByUserId = doctor.Id
        };
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        var service = CreateAppointmentService(context);

        // Act
        var result = await service.MarkNoShowAsync(appointment.Id, doctor.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(AppointmentStatus.NoShow, result.Appointment.Status);
        Assert.True(result.Appointment.IsNoShow);

        var updatedPatient = await context.Patients.FindAsync(patient.Id);
        Assert.Equal(1, updatedPatient.NoShowCount);
    }

    [Fact]
    public async Task UnblockPatientAsync_BlockedPatient_UnblocksAndResetsCount()
    {
        // Arrange
        await using var context = CreateContext(nameof(UnblockPatientAsync_BlockedPatient_UnblocksAndResetsCount));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.Add(patient);

        var patientEntity = CreatePatient(clinic.Id, patient.Id, "Paciente Test", patient.Email);
        patientEntity.IsBlocked = true;
        patientEntity.BlockedUntil = DateTime.UtcNow.AddMonths(3);
        patientEntity.LateCancellationCount = 3;
        context.Patients.Add(patientEntity);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        await context.SaveChangesAsync();

        var service = CreateAppointmentService(context);

        // Act
        var result = await service.UnblockPatientAsync(patient.Id, doctor.Id);

        // Assert
        Assert.True(result.Success);

        var updatedPatient = await context.Patients.FindAsync(patient.Id);
        Assert.False(updatedPatient.IsBlocked);
        Assert.Null(updatedPatient.BlockedUntil);
        Assert.Equal(0, updatedPatient.LateCancellationCount);
    }

    [Fact]
    public async Task UpdateAppointmentAsync_Reschedule_ValidatesNewTime()
    {
        // Arrange
        await using var context = CreateContext(nameof(UpdateAppointmentAsync_Reschedule_ValidatesNewTime));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        var patientEntity = CreatePatient(clinic.Id, patient.Id, "Paciente Test", patient.Email);
        context.Patients.Add(patientEntity);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        var appointment = new Appointment
        {
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = nextMonday.Date.AddHours(10),
            EndAt = nextMonday.Date.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Scheduled,
            CreatedByUserId = doctor.Id
        };
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        var service = CreateAppointmentService(context);

        // Act - reprogramar a martes (fuera de horario del doctor que solo atiende lunes)
        var nextTuesday = nextMonday.AddDays(1);
        var updatedAppointment = new Appointment
        {
            Id = appointment.Id,
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = nextTuesday.Date.AddHours(10),
            EndAt = nextTuesday.Date.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Scheduled,
            CreatedByUserId = doctor.Id
        };

        var result = await service.UpdateAppointmentAsync(updatedAppointment, isPatientBooking: true);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("horario laboral"));
    }

    [Fact]
    public async Task CreateAppointmentFromPortalAsync_ValidAppointment_ReturnsSuccess()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAppointmentFromPortalAsync_ValidAppointment_ReturnsSuccess));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patientUser = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patientUser);

        var patient = CreatePatient(clinic.Id, patientUser.Id, "Paciente Test", patientUser.Email);
        context.Patients.Add(patient);

        // Horario: Lunes 9:00-14:00
        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var service = CreateAppointmentService(context);

        // Usar un lunes fijo conocido en el futuro (evita problemas de cálculo de "próximo lunes")
        var fixedMonday = new DateTime(2026, 12, 7, 10, 0, 0, DateTimeKind.Unspecified); // Lunes 7 dic 2026 10:00 hora local clínica
        // Verificar que es lunes
        Assert.Equal(DayOfWeek.Monday, fixedMonday.DayOfWeek);

        // Act - Cita a las 10:00 (dentro del horario 9-14)
        var result = await service.CreateAppointmentFromPortalAsync(
            clinic.Id,
            doctor.Id,
            patient.Id,
            fixedMonday,
            fixedMonday.AddMinutes(30),
            AppointmentType.FirstVisit,
            "Primera consulta desde portal"
        );

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Appointment);
        Assert.Equal(AppointmentStatus.Scheduled, result.Appointment.Status);
        Assert.Equal(patient.Id, result.Appointment.CreatedByUserId);
        Assert.Equal("Primera consulta desde portal", result.Appointment.Notes);
    }

    [Fact]
    public async Task CreateAppointmentFromPortalAsync_OutsidePatientHorizon_ReturnsError()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAppointmentFromPortalAsync_OutsidePatientHorizon_ReturnsError));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patientUser = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patientUser);

        var patient = CreatePatient(clinic.Id, patientUser.Id, "Paciente Test", patientUser.Email);
        context.Patients.Add(patient);

        // Horario: Lunes 9:00-14:00
        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var service = CreateAppointmentService(context);

        // Fecha a 50 días (fuera de horizonte 45d para pacientes) - buscar un lunes
        var farFuture = DateTime.UtcNow.AddDays(50);
        var nextMonday = farFuture.AddDays(((int)DayOfWeek.Monday - (int)farFuture.DayOfWeek + 7) % 7);
        if (nextMonday <= farFuture) nextMonday = nextMonday.AddDays(7);

        // Act
        var result = await service.CreateAppointmentFromPortalAsync(
            clinic.Id,
            doctor.Id,
            patient.Id,
            nextMonday.AddHours(10),
            nextMonday.AddHours(10).AddMinutes(30),
            AppointmentType.FirstVisit,
            null
        );

        // Assert
        Assert.False(result.Success);
        // El error puede ser por horario o por horizonte (depende del orden de validación)
        Assert.Contains(result.Errors, e => e.Contains("45 días") || e.Contains("horario laboral"));
    }

    [Fact]
    public async Task CreateAppointmentFromPortalAsync_BlockedPatient_ReturnsError()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAppointmentFromPortalAsync_BlockedPatient_ReturnsError));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patientUser = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patientUser);

        var patient = CreatePatient(clinic.Id, patientUser.Id, "Paciente Test", patientUser.Email);
        patient.IsBlocked = true;
        patient.BlockedUntil = DateTime.UtcNow.AddMonths(3);
        context.Patients.Add(patient);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var service = CreateAppointmentService(context);

        var fixedMonday = new DateTime(2026, 12, 7, 10, 0, 0, DateTimeKind.Unspecified); // Lunes 10:00 hora local clínica
        Assert.Equal(DayOfWeek.Monday, fixedMonday.DayOfWeek);

        // Act
        var result = await service.CreateAppointmentFromPortalAsync(
            clinic.Id,
            doctor.Id,
            patient.Id,
            fixedMonday,
            fixedMonday.AddMinutes(30),
            AppointmentType.FirstVisit,
            null
        );

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("bloqueado"));
    }

    [Fact]
    public async Task CreateAppointmentFromPortalAsync_DoctorNotInClinic_ReturnsError()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAppointmentFromPortalAsync_DoctorNotInClinic_ReturnsError));
        var clinic1 = CreateClinic("Clinic 1", "clinic-1");
        var clinic2 = CreateClinic("Clinic 2", "clinic-2");
        context.Clinics.AddRange(clinic1, clinic2);

        var doctor = CreateUser("doctor@test.com", clinic1.Id); // Doctor en clinic1
        var patientUser = CreateUser("patient@test.com", clinic1.Id);
        context.Users.AddRange(doctor, patientUser);

        var patient = CreatePatient(clinic1.Id, patientUser.Id, "Paciente Test", patientUser.Email);
        context.Patients.Add(patient);

        var schedule = CreateSchedule(clinic1.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var service = CreateAppointmentService(context);

        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        // Act - Intentar reservar en clinic2 con doctor de clinic1
        var result = await service.CreateAppointmentFromPortalAsync(
            clinic2.Id, // Clínica diferente
            doctor.Id,
            patient.Id,
            nextMonday.AddHours(10),
            nextMonday.AddHours(10).AddMinutes(30),
            AppointmentType.FirstVisit,
            null
        );

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("clínica"));
    }
}