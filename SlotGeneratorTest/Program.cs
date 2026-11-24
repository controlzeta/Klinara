using System;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Klinara_Test;Trusted_Connection=True;TrustServerCertificate=True"));

builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();

// Configurar SlotGenerator para test
builder.Services.Configure<SlotGeneratorOptions>(o =>
{
    o.Enabled = true;
    o.DaysAhead = 14;
    o.RunAtUtc = TimeSpan.FromHours(3);
});

builder.Services.AddScoped<SlotGeneratorService>();

using var host = builder.Build();

using var scope = host.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var slotGenerator = scope.ServiceProvider.GetRequiredService<SlotGeneratorService>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("=== Test SlotGenerator ===");

// Limpiar datos previos
context.TimeSlots.RemoveRange(context.TimeSlots);
context.DoctorSchedules.RemoveRange(context.DoctorSchedules);
context.ClinicHolidays.RemoveRange(context.ClinicHolidays);
context.DoctorVacations.RemoveRange(context.DoctorVacations);
context.Appointments.RemoveRange(context.Appointments);
context.Patients.RemoveRange(context.Patients);
context.Users.RemoveRange(context.Users);
context.Clinics.RemoveRange(context.Clinics);
await context.SaveChangesAsync();

// Crear clínica
var clinic = new Clinic
{
    Id = Guid.NewGuid(),
    Name = "Clínica SlotGen Test",
    Subdomain = "slotgen-test",
    ContactEmail = "admin@slotgen-test.com",
    TimeZoneId = "America/Mexico_City",
    IsActive = true,
    CreatedAt = DateTime.UtcNow
};
context.Clinics.Add(clinic);

// Crear doctor
var doctorUser = new ApplicationUser
{
    Id = Guid.NewGuid(),
    UserName = "doctor@slotgen.com",
    Email = "doctor@slotgen.com",
    EmailConfirmed = true,
    ClinicId = clinic.Id
};
context.Users.Add(doctorUser);

// Crear horario: Lunes a Viernes 9-14, lunch 13-14, slots 30 min
var schedules = new[]
{
    new DoctorSchedule { ClinicId = clinic.Id, DoctorId = doctorUser.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(9,0), EndTime = new TimeOnly(14,0), LunchStart = new TimeOnly(13,0), LunchEnd = new TimeOnly(14,0), SlotDurationMinutes = 30, IsActive = true, EffectiveFrom = DateTime.UtcNow.Date.AddDays(-10), CreatedAt = DateTime.UtcNow },
    new DoctorSchedule { ClinicId = clinic.Id, DoctorId = doctorUser.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(9,0), EndTime = new TimeOnly(14,0), LunchStart = new TimeOnly(13,0), LunchEnd = new TimeOnly(14,0), SlotDurationMinutes = 30, IsActive = true, EffectiveFrom = DateTime.UtcNow.Date.AddDays(-10), CreatedAt = DateTime.UtcNow },
    new DoctorSchedule { ClinicId = clinic.Id, DoctorId = doctorUser.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(9,0), EndTime = new TimeOnly(14,0), LunchStart = new TimeOnly(13,0), LunchEnd = new TimeOnly(14,0), SlotDurationMinutes = 30, IsActive = true, EffectiveFrom = DateTime.UtcNow.Date.AddDays(-10), CreatedAt = DateTime.UtcNow },
    new DoctorSchedule { ClinicId = clinic.Id, DoctorId = doctorUser.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeOnly(9,0), EndTime = new TimeOnly(14,0), LunchStart = new TimeOnly(13,0), LunchEnd = new TimeOnly(14,0), SlotDurationMinutes = 30, IsActive = true, EffectiveFrom = DateTime.UtcNow.Date.AddDays(-10), CreatedAt = DateTime.UtcNow },
    new DoctorSchedule { ClinicId = clinic.Id, DoctorId = doctorUser.Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeOnly(9,0), EndTime = new TimeOnly(14,0), LunchStart = new TimeOnly(13,0), LunchEnd = new TimeOnly(14,0), SlotDurationMinutes = 30, IsActive = true, EffectiveFrom = DateTime.UtcNow.Date.AddDays(-10), CreatedAt = DateTime.UtcNow },
};
context.DoctorSchedules.AddRange(schedules);

// Crear festivo: próximo miércoles día completo
var nextWednesday = DateTime.UtcNow.AddDays(((int)DayOfWeek.Wednesday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
if (nextWednesday <= DateTime.UtcNow) nextWednesday = nextWednesday.AddDays(7);

var holiday = new ClinicHoliday
{
    Id = Guid.NewGuid(),
    ClinicId = clinic.Id,
    Name = "Festivo Test",
    Date = nextWednesday.Date,
    IsRecurring = false,
    IsFullDay = true,
    CreatedAt = DateTime.UtcNow
};
context.ClinicHolidays.Add(holiday);

// Crear vacaciones: próximo jueves-viernes
var vacation = new DoctorVacation
{
    Id = Guid.NewGuid(),
    ClinicId = clinic.Id,
    DoctorId = doctorUser.Id,
    StartDate = nextWednesday.AddDays(1).Date, // Jueves
    EndDate = nextWednesday.AddDays(2).Date,  // Viernes
    Reason = "Vacaciones test",
    CreatedAt = DateTime.UtcNow
};
context.DoctorVacations.Add(vacation);

await context.SaveChangesAsync();

logger.LogInformation("Datos de prueba creados. Ejecutando SlotGenerator...");

// Ejecutar generación
await slotGenerator.GenerateSlotsAsync();

// Verificar resultados
var slots = await context.TimeSlots
    .Where(s => s.ClinicId == clinic.Id && s.DoctorId == doctorUser.Id)
    .OrderBy(s => s.StartAt)
    .ToListAsync();

logger.LogInformation("Total slots generados: {Count}", slots.Count);

var byDate = slots.GroupBy(s => s.StartAt.Date).OrderBy(g => g.Key);
foreach (var day in byDate)
{
    var booked = day.Count(s => s.IsBooked);
    var blocked = day.Count(s => s.IsBlocked);
    var free = day.Count() - booked - blocked;
    logger.LogInformation("  {Date:ddd dd/MM}: {Total} slots ({Free} libres, {Booked} reservados, {Blocked} bloqueados)", 
        day.Key, day.Count(), free, booked, blocked);
    
    // Verificar que no hay slots en festivo (miércoles)
    if (day.Key.Date == nextWednesday.Date)
    {
        if (day.Any())
            logger.LogWarning("  ⚠️ FESTIVO: Se generaron slots en día festivo!");
        else
            logger.LogInformation("  ✅ FESTIVO: Correctamente sin slots");
    }
    
    // Verificar que no hay slots en vacaciones (jueves-viernes)
    if (day.Key.Date == nextWednesday.AddDays(1).Date || day.Key.Date == nextWednesday.AddDays(2).Date)
    {
        if (day.Any())
            logger.LogWarning("  ⚠️ VACACIONES: Se generaron slots en vacaciones!");
        else
            logger.LogInformation("  ✅ VACACIONES: Correctamente sin slots");
    }
    
    // Verificar que no hay slots en lunch (13:00-14:00)
    var lunchSlots = day.Where(s => s.StartAt.TimeOfDay >= TimeSpan.FromHours(13) && s.StartAt.TimeOfDay < TimeSpan.FromHours(14));
    if (lunchSlots.Any())
        logger.LogWarning("  ⚠️ LUNCH: Se generaron slots en horario de comida!");
    else
        logger.LogInformation("  ✅ LUNCH: Correctamente sin slots en comida");
}

// Verificar idempotencia: ejecutar de nuevo no debe duplicar
logger.LogInformation("\n--- Probando idempotencia (segunda ejecución) ---");
await slotGenerator.GenerateSlotsAsync();

var slotsAfter = await context.TimeSlots
    .Where(s => s.ClinicId == clinic.Id && s.DoctorId == doctorUser.Id)
    .CountAsync();

logger.LogInformation("Slots después de 2da ejecución: {Count} (esperado: {Expected})", slotsAfter, slots.Count);

if (slotsAfter == slots.Count)
    logger.LogInformation("✅ IDEMPOTENCIA: Correcto - no se duplicaron slots");
else
    logger.LogError("❌ IDEMPOTENCIA: Falló - se crearon {Diff} slots extra", slotsAfter - slots.Count);

logger.LogInformation("=== Test SlotGenerator COMPLETADO ===");