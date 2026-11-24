using System;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Klinara.Tests;

public class ScheduleServiceDebugTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;
        return new ApplicationDbContext(options);
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

    private static ScheduleService CreateScheduleService(ApplicationDbContext context)
    {
        var logger = new LoggerFactory().CreateLogger<ScheduleService>();
        return new ScheduleService(context, logger);
    }

    [Fact]
    public async Task Debug_ValidateAppointment_WithinSchedule()
    {
        // Arrange
        await using var context = CreateContext(nameof(Debug_ValidateAppointment_WithinSchedule));
        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var doctor = CreateUser("doctor@test.com", clinic.Id);
        var patient = CreateUser("patient@test.com", clinic.Id);
        context.Users.AddRange(doctor, patient);

        // Crear entidad Patient para el paciente
        var patientEntity = new Patient
        {
            Id = patient.Id,
            ClinicId = clinic.Id,
            FullName = "Paciente Test",
            Email = patient.Email,
            CreatedByUserId = patient.Id
        };
        context.Patients.Add(patientEntity);

        var schedule = CreateSchedule(clinic.Id, doctor.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0), new TimeOnly(13, 0), new TimeOnly(14, 0));
        context.DoctorSchedules.Add(schedule);

        await context.SaveChangesAsync();

        var service = CreateScheduleService(context);

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

        // Act
        var result = await service.ValidateAppointmentAsync(appointment, isPatientBooking: true);

        // Debug output
        Console.WriteLine($"IsValid: {result.IsValid}");
        Console.WriteLine($"Errors: {string.Join(", ", result.Errors)}");
        Console.WriteLine($"Warnings: {string.Join(", ", result.Warnings)}");

        // Check schedule retrieval
        var schedules = await service.GetDoctorSchedulesAsync(clinic.Id, doctor.Id, appointment.StartAt);
        Console.WriteLine($"Schedules found: {schedules.Count}");
        foreach (var s in schedules)
        {
            Console.WriteLine($"  Schedule: DayOfWeek={s.DayOfWeek}, StartTime={s.StartTime}, EndTime={s.EndTime}, EffectiveFrom={s.EffectiveFrom}");
        }

        // Check holiday
        var isHoliday = await service.IsClinicHolidayAsync(clinic.Id, appointment.StartAt);
        Console.WriteLine($"IsHoliday: {isHoliday}");

        // Check vacation
        var isVacation = await service.IsDoctorOnVacationAsync(doctor.Id, appointment.StartAt);
        Console.WriteLine($"IsVacation: {isVacation}");

        // Check horizon
        var hoursDiff = (appointment.StartAt - DateTime.UtcNow).TotalHours;
        Console.WriteLine($"Hours until appointment: {hoursDiff}");

        Assert.True(result.IsValid, $"Validation failed: {string.Join("; ", result.Errors)}");
    }
}