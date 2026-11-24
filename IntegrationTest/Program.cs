using System;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Klinara_Test;Trusted_Connection=True;TrustServerCertificate=True"));

builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();

using var host = builder.Build();

using var scope = host.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var scheduleService = scope.ServiceProvider.GetRequiredService<IScheduleService>();
var appointmentService = scope.ServiceProvider.GetRequiredService<IAppointmentService>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("=== Test de Integración con BD Local ===");

// 1. Verificar BD conectada
var canConnect = await context.Database.CanConnectAsync();
logger.LogInformation("BD conectada: {CanConnect}", canConnect);

// 2. Crear clínica de prueba
var clinic = new Clinic
{
    Id = Guid.NewGuid(),
    Name = "Clínica Test Integración",
    Subdomain = "test-integracion",
    ContactEmail = "admin@test-integracion.com",
    Phone = "555-1234",
    Address = "Av. Test 123",
    TimeZoneId = "America/Mexico_City",
    IsActive = true,
    CreatedAt = DateTime.UtcNow
};
context.Clinics.Add(clinic);
await context.SaveChangesAsync();
logger.LogInformation("Clínica creada: {ClinicId}", clinic.Id);

// 3. Crear doctor y paciente (ApplicationUser + Patient)
var doctorUser = new ApplicationUser
{
    Id = Guid.NewGuid(),
    UserName = "doctor@test.com",
    Email = "doctor@test.com",
    EmailConfirmed = true,
    ClinicId = clinic.Id
};
context.Users.Add(doctorUser);

var patientUser = new ApplicationUser
{
    Id = Guid.NewGuid(),
    UserName = "patient@test.com",
    Email = "patient@test.com",
    EmailConfirmed = true,
    ClinicId = clinic.Id
};
context.Users.Add(patientUser);

var patient = new Patient
{
    Id = patientUser.Id,
    ClinicId = clinic.Id,
    FullName = "Juan Pérez",
    Email = "patient@test.com",
    Phone = "555-5678",
    DateOfBirth = new DateTime(1990, 5, 15),
    Gender = "M",
    CreatedByUserId = doctorUser.Id,
    CreatedAt = DateTime.UtcNow
};
context.Patients.Add(patient);

await context.SaveChangesAsync();
logger.LogInformation("Doctor: {DoctorId}, Paciente: {PatientId}", doctorUser.Id, patientUser.Id);

// 4. Crear horario doctor (lunes 9-14, lunch 13-14)
var schedule = new DoctorSchedule
{
    Id = Guid.NewGuid(),
    ClinicId = clinic.Id,
    DoctorId = doctorUser.Id,
    DayOfWeek = DayOfWeek.Monday,
    StartTime = new TimeOnly(9, 0),
    EndTime = new TimeOnly(14, 0),
    LunchStart = new TimeOnly(13, 0),
    LunchEnd = new TimeOnly(14, 0),
    SlotDurationMinutes = 30,
    IsActive = true,
    EffectiveFrom = DateTime.UtcNow.Date.AddDays(-30),
    CreatedAt = DateTime.UtcNow
};
context.DoctorSchedules.Add(schedule);
await context.SaveChangesAsync();
logger.LogInformation("Horario doctor creado: {ScheduleId}", schedule.Id);

// 5. Probar GetDoctorSchedulesAsync
var nextMonday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
if (nextMonday <= DateTime.UtcNow) nextMonday = nextMonday.AddDays(7);

var schedules = await scheduleService.GetDoctorSchedulesAsync(clinic.Id, doctorUser.Id, nextMonday.Date.AddHours(10));
logger.LogInformation("Horarios encontrados para próximo lunes: {Count}", schedules.Count);
foreach (var s in schedules)
    logger.LogInformation("  - DayOfWeek: {Day}, Start: {Start}, End: {End}, Lunch: {LunchStart}-{LunchEnd}", s.DayOfWeek, s.StartTime, s.EndTime, s.LunchStart, s.LunchEnd);

// 6. Probar GenerateSlotsForPeriodAsync
var startDate = nextMonday.Date;
var endDate = startDate.AddDays(7);
var slots = await scheduleService.GenerateSlotsForPeriodAsync(clinic.Id, doctorUser.Id, startDate, endDate);
logger.LogInformation("Slots generados para 1 semana: {Count}", slots.Count);
var bookedSlots = slots.Count(s => s.IsBooked);
var blockedSlots = slots.Count(s => s.IsBlocked);
logger.LogInformation("  - Libres: {Free}, Reservados: {Booked}, Bloqueados: {Blocked}", slots.Count - bookedSlots - blockedSlots, bookedSlots, blockedSlots);

// 7. Probar IsClinicHolidayAsync / IsDoctorOnVacationAsync
var isHoliday = await scheduleService.IsClinicHolidayAsync(clinic.Id, nextMonday);
var isVacation = await scheduleService.IsDoctorOnVacationAsync(doctorUser.Id, nextMonday);
logger.LogInformation("Es festivo: {Holiday}, En vacaciones: {Vacation}", isHoliday, isVacation);

// 8. Probar ValidateAppointmentAsync - cita válida
var validAppointment = new Appointment
{
    Id = Guid.NewGuid(),
    ClinicId = clinic.Id,
    DoctorId = doctorUser.Id,
    PatientId = patientUser.Id,
    StartAt = nextMonday.Date.AddHours(10), // Lunes 10:00 (dentro de horario, antes de lunch)
    EndAt = nextMonday.Date.AddHours(10).AddMinutes(30),
    CreatedByUserId = doctorUser.Id
};

var validation = await scheduleService.ValidateAppointmentAsync(validAppointment, isPatientBooking: true);
logger.LogInformation("Validación cita válida: {IsValid}, Errors: {Errors}", validation.IsValid, string.Join("; ", validation.Errors));

// 9. Probar CreateAppointmentAsync
var createResult = await appointmentService.CreateAppointmentAsync(validAppointment, isPatientBooking: true);
logger.LogInformation("Crear cita: {Success}, AppointmentId: {Id}", createResult.Success, createResult.Appointment?.Id);

// 10. Probar cita en lunch (debe fallar)
var lunchAppointment = new Appointment
{
    Id = Guid.NewGuid(),
    ClinicId = clinic.Id,
    DoctorId = doctorUser.Id,
    PatientId = patientUser.Id,
    StartAt = nextMonday.Date.AddHours(13).AddMinutes(15), // Lunes 13:15 (en lunch)
    EndAt = nextMonday.Date.AddHours(13).AddMinutes(45),
    CreatedByUserId = doctorUser.Id
};

var lunchValidation = await scheduleService.ValidateAppointmentAsync(lunchAppointment, isPatientBooking: true);
logger.LogInformation("Validación cita en lunch: {IsValid}, Errors: {Errors}", lunchValidation.IsValid, string.Join("; ", lunchValidation.Errors));

// 11. Probar cita fuera de horizonte (>45 días) para paciente
var farFuture = DateTime.UtcNow.AddDays(50);
var nextMondayFar = farFuture.AddDays(((int)DayOfWeek.Monday - (int)farFuture.DayOfWeek + 7) % 7);
if (nextMondayFar <= farFuture) nextMondayFar = nextMondayFar.AddDays(7);

var farAppointment = new Appointment
{
    Id = Guid.NewGuid(),
    ClinicId = clinic.Id,
    DoctorId = doctorUser.Id,
    PatientId = patientUser.Id,
    StartAt = nextMondayFar.Date.AddHours(10),
    EndAt = nextMondayFar.Date.AddHours(10).AddMinutes(30),
    CreatedByUserId = doctorUser.Id
};

var farValidation = await scheduleService.ValidateAppointmentAsync(farAppointment, isPatientBooking: true);
logger.LogInformation("Validación cita >45d (paciente): {IsValid}, Errors: {Errors}", farValidation.IsValid, string.Join("; ", farValidation.Errors));

// 12. Probar cita >45d para staff (debe pasar)
var farValidationStaff = await scheduleService.ValidateAppointmentAsync(farAppointment, isPatientBooking: false);
logger.LogInformation("Validación cita >45d (staff): {IsValid}, Errors: {Errors}", farValidationStaff.IsValid, string.Join("; ", farValidationStaff.Errors));

// 13. Probar cita en festivo (crear festivo y validar)
var holiday = new ClinicHoliday
{
    Id = Guid.NewGuid(),
    ClinicId = clinic.Id,
    Name = "Día de Muertos",
    Date = nextMonday.Date,
    IsRecurring = false,
    IsFullDay = true,
    CreatedAt = DateTime.UtcNow
};
context.ClinicHolidays.Add(holiday);
await context.SaveChangesAsync();

var holidayValidation = await scheduleService.ValidateAppointmentAsync(validAppointment, isPatientBooking: true);
logger.LogInformation("Validación cita en festivo: {IsValid}, Errors: {Errors}", holidayValidation.IsValid, string.Join("; ", holidayValidation.Errors));

// 14. Probar CancelAppointmentAsync (cancelación tardía <24h)
var nearAppointment = new Appointment
{
    Id = Guid.NewGuid(),
    ClinicId = clinic.Id,
    DoctorId = doctorUser.Id,
    PatientId = patientUser.Id,
    StartAt = DateTime.UtcNow.AddHours(12), // En 12 horas
    EndAt = DateTime.UtcNow.AddHours(12).AddMinutes(30),
    Status = AppointmentStatus.Scheduled,
    CreatedByUserId = doctorUser.Id
};
context.Appointments.Add(nearAppointment);
await context.SaveChangesAsync();

var cancelResult = await appointmentService.CancelAppointmentAsync(nearAppointment.Id, patientUser.Id, "No puedo ir", isPatientCancellation: true);
logger.LogInformation("Cancelar cita <24h: {Success}, IsLateCancellation: {Late}, Status: {Status}", cancelResult.Success, cancelResult.Appointment?.IsLateCancellation, cancelResult.Appointment?.Status);

var updatedPatient = await context.Patients.FindAsync(patientUser.Id);
logger.LogInformation("Paciente LateCancellationCount: {Count}, IsBlocked: {Blocked}", updatedPatient.LateCancellationCount, updatedPatient.IsBlocked);

// 15. Probar MarkNoShowAsync
var noshowAppointment = new Appointment
{
    Id = Guid.NewGuid(),
    ClinicId = clinic.Id,
    DoctorId = doctorUser.Id,
    PatientId = patientUser.Id,
    StartAt = nextMonday.Date.AddHours(11),
    EndAt = nextMonday.Date.AddHours(11).AddMinutes(30),
    Status = AppointmentStatus.Scheduled,
    CreatedByUserId = doctorUser.Id
};
context.Appointments.Add(noshowAppointment);
await context.SaveChangesAsync();

var noshowResult = await appointmentService.MarkNoShowAsync(noshowAppointment.Id, doctorUser.Id);
logger.LogInformation("Marcar no-show: {Success}, IsNoShow: {NoShow}", noshowResult.Success, noshowResult.Appointment?.IsNoShow);

updatedPatient = await context.Patients.FindAsync(patientUser.Id);
logger.LogInformation("Paciente NoShowCount: {Count}", updatedPatient.NoShowCount);

// 16. Verificar TimeSlots generados/liberados
var finalSlots = await context.TimeSlots.Where(s => s.ClinicId == clinic.Id).ToListAsync();
logger.LogInformation("TimeSlots totales en BD: {Count}", finalSlots.Count);
foreach (var s in finalSlots.Where(x => x.IsBooked || x.AppointmentId != null))
    logger.LogInformation("  - Slot {Id}: Booked={Booked}, ApptId={ApptId}", s.Id, s.IsBooked, s.AppointmentId);

logger.LogInformation("=== Test de Integración COMPLETADO ===");