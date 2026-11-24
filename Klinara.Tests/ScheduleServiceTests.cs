using System;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Klinara.Tests;

public class ScheduleServiceTests
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
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            ClinicId = clinicId
        };
        return user;
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
            EffectiveFrom = DateTime.UtcNow.Date.AddDays(-30) // Usar solo fecha (medianoche) para evitar problemas de zona horaria
        };
    }

    private static ScheduleService CreateScheduleService(ApplicationDbContext context)
    {
        var logger = new LoggerFactory().CreateLogger<ScheduleService>();
        return new ScheduleService(context, logger);
    }

    [Fact]
    public async Task ValidateAppointmentAsync_ValidAppointment_ReturnsValid()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateAppointmentAsync_ValidAppointment_ReturnsValid));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        var patientEntity = new Patient
        {
            Id = patient.Id,
            ClinicId = clinic.Id,
            FullName = "Paciente Test",
            Email = patient.Email
        };
        context.Patients.Add(patientEntity);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0), new TimeOnly(13, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = DateTime.UtcNow.AddDays(1).Date.AddHours(10), // Martes 10:00 - fuera del horario (lunes)
            EndAt = DateTime.UtcNow.AddDays(1).Date.AddHours(10).AddMinutes(30)
        };

        var service = CreateScheduleService(context);

        // Act - cita fuera de horario (lunes vs martes)
        var result = await service.ValidateAppointmentAsync(appointment, isPatientBooking: true);

        // Assert - debe fallar por fuera de horario
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("horario laboral"));
    }

    [Fact]
    public async Task ValidateAppointmentAsync_WithinSchedule_ReturnsValid()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateAppointmentAsync_WithinSchedule_ReturnsValid));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        var patientEntity = new Patient
        {
            Id = patient.Id,
            ClinicId = clinic.Id,
            FullName = "Paciente Test",
            Email = patient.Email
        };
        context.Patients.Add(patientEntity);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0), new TimeOnly(13, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        var appointment = new Appointment
                    {
                        Id = Guid.NewGuid(),
                        ClinicId = clinic.Id,
                        DoctorId = doctor.Id,
                        PatientId = patient.Id,
                        StartAt = nextMonday.Date.AddHours(10), // Lunes 10:00
                        EndAt = nextMonday.Date.AddHours(10).AddMinutes(30)
                    };

        var service = CreateScheduleService(context);

        // Act
        var result = await service.ValidateAppointmentAsync(appointment, isPatientBooking: true);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAppointmentAsync_OverlappingAppointment_ReturnsInvalid()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateAppointmentAsync_OverlappingAppointment_ReturnsInvalid));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient1 = CreateUser("patient1@test.com", clinic.Id);
        var patient2 = CreateUser("patient2@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient1, patient2);

        var p1 = new Patient { Id = patient1.Id, ClinicId = clinic.Id, FullName = "Paciente 1", Email = patient1.Email };
        var p2 = new Patient { Id = patient2.Id, ClinicId = clinic.Id, FullName = "Paciente 2", Email = patient2.Email };
        context.Patients.AddRange(p1, p2);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        // Primera cita existente
        var existingAppt = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient1.Id,
            StartAt = nextMonday.AddHours(10),
            EndAt = nextMonday.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Scheduled
        };
        context.Appointments.Add(existingAppt);

        await context.SaveChangesAsync();

        // Segunda cita que se traslape
        var overlappingAppt = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient2.Id,
            StartAt = nextMonday.AddHours(10).AddMinutes(15), // Se traslape 15 min
            EndAt = nextMonday.AddHours(10).AddMinutes(45),
            Status = AppointmentStatus.Scheduled
        };

        var service = CreateScheduleService(context);

        // Act
        var result = await service.ValidateAppointmentAsync(overlappingAppt, isPatientBooking: true);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("traslape"));
    }

    [Fact]
    public async Task ValidateAppointmentAsync_LunchTime_ReturnsInvalid()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateAppointmentAsync_LunchTime_ReturnsInvalid));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        var patientEntity = new Patient { Id = patient.Id, ClinicId = clinic.Id, FullName = "Paciente Test", Email = patient.Email };
        context.Patients.Add(patientEntity);

        // Horario con comida 13:00-14:00
        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0), new TimeOnly(13, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

        // Cita que cae en horario de comida (13:00-13:30)
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = nextMonday.Date.AddHours(13), // 13:00 - inicio de comida
            EndAt = nextMonday.Date.AddHours(13).AddMinutes(30)
        };

        var service = CreateScheduleService(context);

        // Act
        var result = await service.ValidateAppointmentAsync(appointment, isPatientBooking: true);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("comida"));
    }

    [Fact]
    public async Task ValidateAppointmentAsync_PatientBookingHorizon_Enforced()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateAppointmentAsync_PatientBookingHorizon_Enforced));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        var patientEntity = new Patient { Id = patient.Id, ClinicId = clinic.Id, FullName = "Paciente Test", Email = patient.Email };
        context.Patients.Add(patientEntity);

        // Horario lunes 9-14
        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        // Calcular próximo lunes dentro de 50 días (más de 45 días)
        var farFuture = DateTime.UtcNow.AddDays(50);
        var nextMonday = farFuture.AddDays(((int)DayOfWeek.Monday - (int)farFuture.DayOfWeek + 7) % 7);
        if (nextMonday <= farFuture) nextMonday = nextMonday.AddDays(7);

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = nextMonday.Date.AddHours(10), // Lunes a las 10:00 (dentro del horario)
            EndAt = nextMonday.Date.AddHours(10).AddMinutes(30)
        };

        var service = CreateScheduleService(context);

        // Act - reserva de paciente (isPatientBooking = true)
        var result = await service.ValidateAppointmentAsync(appointment, isPatientBooking: true);

        // Assert - debe fallar por horizonte > 45 días
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("45 días"));
    }

    [Fact]
    public async Task ValidateAppointmentAsync_StaffBooking_NoHorizonLimit()
    {
        // Arrange
        await using var context = CreateContext(nameof(ValidateAppointmentAsync_StaffBooking_NoHorizonLimit));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        var patientEntity = new Patient { Id = patient.Id, ClinicId = clinic.Id, FullName = "Paciente Test", Email = patient.Email };
        context.Patients.Add(patientEntity);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        // Calcular próximo lunes dentro de 50 días
        var farFuture = DateTime.UtcNow.AddDays(50);
        var nextMonday = farFuture.AddDays(((int)DayOfWeek.Monday - (int)farFuture.DayOfWeek + 7) % 7);
        if (nextMonday <= farFuture) nextMonday = nextMonday.AddDays(7);

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = nextMonday.Date.AddHours(10), // Lunes a las 10:00 (dentro del horario)
            EndAt = nextMonday.Date.AddHours(10).AddMinutes(30)
        };

        var service = CreateScheduleService(context);

        // Act - reserva de personal (isPatientBooking = false)
        var result = await service.ValidateAppointmentAsync(appointment, isPatientBooking: false);

        // Assert - personal puede reservar sin límite de horizonte
        Assert.True(result.IsValid);
    }

    [Fact]
        public async Task ValidateAppointmentAsync_ClinicHoliday_ReturnsInvalid()
        {
            // Arrange
            await using var context = CreateContext(nameof(ValidateAppointmentAsync_ClinicHoliday_ReturnsInvalid));
            var clinic = CreateClinic("Test Clinic", "test-clinic");
            context.Clinics.Add(clinic);

            var doctor = CreateUser("doctor@test.com", clinic.Id);
            var patient = CreateUser("patient@test.com", clinic.Id);
            context.Users.AddRange(doctor, patient);

            var patientEntity = new Patient { Id = patient.Id, ClinicId = clinic.Id, FullName = "Paciente Test", Email = patient.Email };
            context.Patients.Add(patientEntity);

            var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
            context.DoctorSchedules.Add(schedule);

            // Asegurar que el festivo caiga en lunes
            var holidayDate = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7).Date;
            var holiday = new ClinicHoliday
            {
                ClinicId = clinic.Id,
                Name = "Festivo",
                Date = holidayDate,
                IsFullDay = true
            };
            context.ClinicHolidays.Add(holiday);

            await context.SaveChangesAsync();

            var appointment = new Appointment
            {
                Id = Guid.NewGuid(),
                ClinicId = clinic.Id,
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                StartAt = holidayDate.AddHours(10),
                EndAt = holidayDate.AddHours(10).AddMinutes(30)
            };

            var service = CreateScheduleService(context);

            // Act
            var result = await service.ValidateAppointmentAsync(appointment, isPatientBooking: true);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("festivo"));
        }

        [Fact]
        public async Task ValidateAppointmentAsync_DoctorVacation_ReturnsInvalid()
        {
            // Arrange
            await using var context = CreateContext(nameof(ValidateAppointmentAsync_DoctorVacation_ReturnsInvalid));
            var clinic = CreateClinic("Test Clinic", "test-clinic");
            context.Clinics.Add(clinic);

            var doctor = CreateUser("doctor@test.com", clinic.Id);
            var patient = CreateUser("patient@test.com", clinic.Id);
            context.Users.AddRange(doctor, patient);

            var patientEntity = new Patient { Id = patient.Id, ClinicId = clinic.Id, FullName = "Paciente Test", Email = patient.Email };
            context.Patients.Add(patientEntity);

            var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
            context.DoctorSchedules.Add(schedule);

            // Asegurar que las vacaciones incluyan un lunes
            var vacationStart = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7).Date;
            var vacation = new DoctorVacation
            {
                ClinicId = clinic.Id,
                DoctorId = doctor.Id,
                StartDate = vacationStart,
                EndDate = vacationStart.AddDays(2)
            };
            context.DoctorVacations.Add(vacation);

            await context.SaveChangesAsync();

            var appointment = new Appointment
            {
                Id = Guid.NewGuid(),
                ClinicId = clinic.Id,
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                StartAt = vacationStart.AddHours(10),
                EndAt = vacationStart.AddHours(10).AddMinutes(30)
            };

            var service = CreateScheduleService(context);

            // Act
            var result = await service.ValidateAppointmentAsync(appointment, isPatientBooking: true);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("vacaciones"));
        }

    [Fact]
    public async Task GenerateSlotsForPeriodAsync_GeneratesCorrectSlots()
    {
        // Arrange
        await using var context = CreateContext(nameof(GenerateSlotsForPeriodAsync_GeneratesCorrectSlots));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), null, null, 60); // 3 slots de 60 min
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        // Periodo de 6 días para tener exactamente 1 lunes
        var startDate = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        var endDate = startDate.AddDays(6);

        var service = CreateScheduleService(context);

        // Act
        var slots = await service.GenerateSlotsForPeriodAsync(clinic.Id, doctor.Id, startDate, endDate);

        // Assert
        Assert.NotEmpty(slots);
        var mondaySlots = slots.Where(s => s.StartAt.DayOfWeek == DayOfWeek.Monday).ToList();
        Assert.Equal(3, mondaySlots.Count); // 9-10, 10-11, 11-12 (3 slots de 60 min)
        Assert.All(mondaySlots, s => Assert.Equal(TimeSpan.FromHours(1), s.EndAt - s.StartAt));
    }

    [Fact]
    public async Task GenerateSlotsForPeriodAsync_ExcludesHolidays()
    {
        // Arrange
        await using var context = CreateContext(nameof(GenerateSlotsForPeriodAsync_ExcludesHolidays));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), null, null, 60);
        context.DoctorSchedules.Add(schedule);

        var holidayDate = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7).Date;
        var holiday = new ClinicHoliday
        {
            ClinicId = clinic.Id,
            Name = "Festivo Test",
            Date = holidayDate,
            IsFullDay = true
        };
        context.ClinicHolidays.Add(holiday);

        await context.SaveChangesAsync();

        var startDate = holidayDate;
        var endDate = startDate.AddDays(7);

        var service = CreateScheduleService(context);

        // Act
        var slots = await service.GenerateSlotsForPeriodAsync(clinic.Id, doctor.Id, startDate, endDate);

        // Assert - no debe haber slots en el día festivo
        var holidaySlots = slots.Where(s => s.StartAt.Date == holidayDate).ToList();
        Assert.Empty(holidaySlots);
    }

    [Fact]
    public async Task GenerateSlotsForPeriodAsync_ExcludesDoctorVacations()
    {
        // Arrange
        await using var context = CreateContext(nameof(GenerateSlotsForPeriodAsync_ExcludesDoctorVacations));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), null, null, 60);
        context.DoctorSchedules.Add(schedule);

        var vacationStart = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7).Date;
        var vacation = new DoctorVacation
        {
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            StartDate = vacationStart,
            EndDate = vacationStart.AddDays(2)
        };
        context.DoctorVacations.Add(vacation);

        await context.SaveChangesAsync();

        var startDate = vacationStart;
        var endDate = startDate.AddDays(7);

        var service = CreateScheduleService(context);

        // Act
        var slots = await service.GenerateSlotsForPeriodAsync(clinic.Id, doctor.Id, startDate, endDate);

        // Assert - no debe haber slots en días de vacaciones
        var vacationSlots = slots.Where(s => s.StartAt.Date >= vacationStart && s.StartAt.Date <= vacationStart.AddDays(2)).ToList();
        Assert.Empty(vacationSlots);
    }

    [Fact]
    public async Task GenerateSlotsForPeriodAsync_ExcludesLunchTime()
    {
        // Arrange
        await using var context = CreateContext(nameof(GenerateSlotsForPeriodAsync_ExcludesLunchTime));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        // Horario 9-14 con comida 13-14, slots 30 min
        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0), new TimeOnly(13, 0), new TimeOnly(14, 0), 30);
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var startDate = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        var endDate = startDate.AddDays(7);

        var service = CreateScheduleService(context);

        // Act
        var slots = await service.GenerateSlotsForPeriodAsync(clinic.Id, doctor.Id, startDate, endDate);

        // Assert - no debe haber slots que caigan en 13:00-14:00
        var mondaySlots = slots.Where(s => s.StartAt.DayOfWeek == DayOfWeek.Monday).ToList();
        var lunchStart = new TimeOnly(13, 0).ToTimeSpan();
        var lunchEnd = new TimeOnly(14, 0).ToTimeSpan();
        var lunchSlots = mondaySlots.Where(s => s.StartAt.TimeOfDay >= lunchStart && s.EndAt.TimeOfDay <= lunchEnd).ToList();
        Assert.Empty(lunchSlots);
    }

    [Fact]
    public async Task IsSlotAvailableAsync_AvailableSlot_ReturnsTrue()
    {
        // Arrange
        await using var context = CreateContext(nameof(IsSlotAvailableAsync_AvailableSlot_ReturnsTrue));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        await context.SaveChangesAsync();

        var service = CreateScheduleService(context);
        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        var end = start.AddMinutes(30);

        // Act
        var available = await service.IsSlotAvailableAsync(clinic.Id, doctor.Id, start, start.AddMinutes(30));

        // Assert
        Assert.True(available);
    }

    [Fact]
    public async Task IsSlotAvailableAsync_BookedSlot_ReturnsFalse()
    {
        // Arrange
        await using var context = CreateContext(nameof(IsSlotAvailableAsync_BookedSlot_ReturnsFalse));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        var patientEntity = new Patient { Id = patient.Id, ClinicId = clinic.Id, FullName = "Paciente", Email = patient.Email };
        context.Patients.Add(patientEntity);

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartAt = DateTime.UtcNow.AddDays(1).Date.AddHours(10),
            EndAt = DateTime.UtcNow.AddDays(1).Date.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Scheduled
        };
        context.Appointments.Add(appointment);

        await context.SaveChangesAsync();

        var service = CreateScheduleService(context);
        var start = appointment.StartAt;
        var end = appointment.EndAt;

        // Act
        var available = await service.IsSlotAvailableAsync(clinic.Id, doctor.Id, start, end);

        // Assert
        Assert.False(available);
    }

    [Fact]
    public async Task GetDoctorSchedulesAsync_ReturnsActiveSchedules()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetDoctorSchedulesAsync_ReturnsActiveSchedules));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        var schedule1 = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        var schedule2 = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.AddRange(schedule1, schedule2);

        // Schedule inactivo
        var inactiveSchedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(14, 0));
        inactiveSchedule.IsActive = false;
        context.DoctorSchedules.Add(inactiveSchedule);

        await context.SaveChangesAsync();

        var service = CreateScheduleService(context);

        // Act
        var schedules = await service.GetDoctorSchedulesAsync(clinic.Id, doctor.Id);

        // Assert
        Assert.Equal(2, schedules.Count);
        Assert.All(schedules, s => Assert.True(s.IsActive));
    }

    [Fact]
    public async Task IsClinicHolidayAsync_ReturnsTrueForHoliday()
    {
        // Arrange
        await using var context = CreateContext(nameof(IsClinicHolidayAsync_ReturnsTrueForHoliday));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var holidayDate = DateTime.UtcNow.AddDays(10).Date;
        var holiday = new ClinicHoliday
        {
            ClinicId = clinic.Id,
            Name = "Festivo",
            Date = holidayDate,
            IsFullDay = true
        };
        context.ClinicHolidays.Add(holiday);

        await context.SaveChangesAsync();

        var service = CreateScheduleService(context);

        // Act
        var isHoliday = await service.IsClinicHolidayAsync(clinic.Id, holidayDate);

        // Assert
        Assert.True(isHoliday);
    }

    [Fact]
    public async Task IsDoctorOnVacationAsync_ReturnsTrueForVacation()
    {
        // Arrange
        await using var context = CreateContext(nameof(IsDoctorOnVacationAsync_ReturnsTrueForVacation));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        context.Users.Add(doctor);

        var vacation = new DoctorVacation
        {
            ClinicId = clinic.Id,
            DoctorId = doctor.Id,
            StartDate = DateTime.UtcNow.AddDays(5).Date,
            EndDate = DateTime.UtcNow.AddDays(10).Date
        };
        context.DoctorVacations.Add(vacation);

        await context.SaveChangesAsync();

        var service = CreateScheduleService(context);
        var vacationDate = DateTime.UtcNow.AddDays(7).Date;

        // Act
        var onVacation = await service.IsDoctorOnVacationAsync(doctor.Id, vacationDate);

        // Assert
        Assert.True(onVacation);
    }
}