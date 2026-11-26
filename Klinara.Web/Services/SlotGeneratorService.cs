using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Klinara.Web.Services;

public class SlotGeneratorOptions
{
    public const string SectionName = "SlotGenerator";

    /// <summary>Días hacia adelante para generar slots (default: 30)</summary>
    public int DaysAhead { get; set; } = 30;

    /// <summary>Hora de ejecución diaria (UTC, default: 03:00)</summary>
    public TimeSpan RunAtUtc { get; set; } = TimeSpan.FromHours(3);

    /// <summary>Habilitar job (default: true)</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Background service que genera TimeSlots diariamente desde DoctorSchedules vigentes.
/// Excluye festivos clínica, vacaciones doctor, y intervalos de comida.
/// </summary>
public class SlotGeneratorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlotGeneratorService> _logger;
    private readonly SlotGeneratorOptions _options;
    private Timer? _timer;

    public SlotGeneratorService(
        IServiceScopeFactory scopeFactory,
        ILogger<SlotGeneratorService> logger,
        IOptions<SlotGeneratorOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("SlotGenerator deshabilitado por configuración");
            return;
        }

        _logger.LogInformation("SlotGenerator iniciado - Ejecutará cada día a las {RunAtUtc} UTC", _options.RunAtUtc);

        // Calcular delay hasta próxima ejecución
        var now = DateTime.UtcNow;
        var nextRun = now.Date.Add(_options.RunAtUtc);
        if (nextRun <= now) nextRun = nextRun.AddDays(1);

        var initialDelay = nextRun - now;
        _logger.LogInformation("Primera ejecución en: {Delay}", initialDelay);

        _timer = new Timer(async _ => await GenerateSlotsAsync(stoppingToken), null, initialDelay, TimeSpan.FromDays(1));

        // Mantener servicio vivo
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SlotGenerator detenido");
        }
        finally
        {
            _timer?.Dispose();
        }
    }

    /// <summary>
    /// Genera slots para todas las clínicas activas y sus doctores con horarios.
    /// </summary>
    public async Task GenerateSlotsAsync(CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid().ToString()[..8];
        _logger.LogInformation("[{RunId}] Iniciando generación de slots", runId);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var startDate = DateTime.UtcNow.Date;
            var endDate = startDate.AddDays(_options.DaysAhead);

            // Obtener clínicas activas con doctores que tienen horarios
            var clinicsWithSchedules = await context.Clinics
                .Where(c => c.IsActive)
                .Join(
                    context.DoctorSchedules.Where(ds => ds.IsActive),
                    c => c.Id,
                    ds => ds.ClinicId,
                    (c, ds) => new { Clinic = c, Schedule = ds })
                .ToListAsync(cancellationToken);

            var grouped = clinicsWithSchedules
                .GroupBy(x => new { x.Clinic.Id, x.Schedule.DoctorId })
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Schedule).ToList());

            int totalSlotsCreated = 0;
            int totalDoctorSchedules = grouped.Count;

            foreach (var kvp in grouped)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            var clinicId = kvp.Key.Id;
                            var doctorId = kvp.Key.DoctorId;
                            var schedules = kvp.Value;

                            foreach (var schedule in schedules)
                            {
                                var created = await GenerateSlotsForDoctorAsync(context, clinicId, schedule, startDate, endDate, cancellationToken);
                                totalSlotsCreated += created;
                            }
                        }

            _logger.LogInformation("[{RunId}] Completado - {DoctorSchedules} horarios doctor, {Slots} slots creados", runId, totalDoctorSchedules, totalSlotsCreated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RunId}] Error generando slots", runId);
        }
    }

    /// <summary>
    /// Genera slots para un doctor específico en un rango de fechas.
    /// </summary>
    private async Task<int> GenerateSlotsForDoctorAsync(
        ApplicationDbContext context,
        Guid clinicId,
        DoctorSchedule schedule,
        DateTime startDateUtc,
        DateTime endDateUtc,
        CancellationToken cancellationToken)
    {
        int created = 0;

        // Obtener zona horaria de la clínica para convertir fechas UTC a local
        var clinic = await context.Clinics.FindAsync(clinicId);
        TimeZoneInfo clinicTimeZone;
        try
        {
            clinicTimeZone = clinic?.TimeZone ?? TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
        }
        catch (System.TimeZoneNotFoundException)
        {
            clinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
        }

        var startDateLocal = TimeZoneInfo.ConvertTimeFromUtc(startDateUtc, clinicTimeZone).Date;
        var endDateLocal = TimeZoneInfo.ConvertTimeFromUtc(endDateUtc, clinicTimeZone).Date;

        // Verificar si el horario está vigente en el período
        if (schedule.EffectiveFrom.Date > endDateLocal) return 0;
        if (schedule.EffectiveTo.HasValue && schedule.EffectiveTo.Value.Date < startDateLocal) return 0;

        var effectiveStart = schedule.EffectiveFrom.Date > startDateLocal ? schedule.EffectiveFrom.Date : startDateLocal;
        var effectiveEnd = schedule.EffectiveTo.HasValue && schedule.EffectiveTo.Value.Date < endDateLocal
            ? schedule.EffectiveTo.Value.Date
            : endDateLocal;

        var effectiveStartDt = effectiveStart;
        var effectiveEndDt = effectiveEnd.AddDays(1).AddTicks(-1);

        // Obtener festivos de la clínica en el período
        var holidays = await context.ClinicHolidays
            .Where(h => h.ClinicId == clinicId
                && h.Date >= effectiveStartDt
                && h.Date <= effectiveEndDt)
            .ToListAsync(cancellationToken);

        var holidayDates = holidays
            .Where(h => h.IsFullDay)
            .Select(h => h.Date.Date)
            .ToHashSet();

        var partialHolidays = holidays
            .Where(h => !h.IsFullDay && h.StartTime.HasValue && h.EndTime.HasValue)
            .ToList();

        // Obtener vacaciones del doctor en el período
        var vacations = await context.DoctorVacations
            .Where(v => v.DoctorId == schedule.DoctorId
                && v.ClinicId == clinicId
                && v.StartDate <= effectiveEndDt
                && v.EndDate >= effectiveStartDt)
            .ToListAsync(cancellationToken);

        // Iterar días en el período efectivo
        for (var date = effectiveStart; date <= effectiveEnd; date = date.AddDays(1))
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Solo generar para el día de la semana del horario
            if (date.DayOfWeek != schedule.DayOfWeek) continue;

            // Saltar festivos día completo
            if (holidayDates.Contains(date.Date)) continue;

            // Saltar vacaciones doctor
            if (vacations.Any(v => date.Date >= v.StartDate.Date && date.Date <= v.EndDate.Date)) continue;

            // Generar slots del día
            var daySlots = GenerateDaySlots(schedule, date, partialHolidays);
            
            foreach (var slot in daySlots)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Verificar si ya existe slot para este doctor/hora
                var exists = await context.TimeSlots
                    .AnyAsync(t => t.ClinicId == clinicId
                        && t.DoctorId == schedule.DoctorId
                        && t.StartAt == slot.StartAt
                        && t.EndAt == slot.EndAt, cancellationToken);

                if (!exists)
                {
                    context.TimeSlots.Add(slot);
                    created++;
                }
            }
        }

        if (created > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    /// <summary>
    /// Genera los slots de un día específico según el horario, excluyendo lunch y festivos parciales.
    /// </summary>
    private static List<TimeSlot> GenerateDaySlots(DoctorSchedule schedule, DateTime date, List<ClinicHoliday> partialHolidays)
    {
        var slots = new List<TimeSlot>();
        var slotDuration = TimeSpan.FromMinutes(schedule.SlotDurationMinutes);
        var current = schedule.StartTime.ToTimeSpan();
        var end = schedule.EndTime.ToTimeSpan();
        var lunchStart = schedule.LunchStart?.ToTimeSpan();
        var lunchEnd = schedule.LunchEnd?.ToTimeSpan();

        while (current + slotDuration <= end)
        {
            var slotStart = current;
            var slotEnd = current + slotDuration;

            // Excluir lunch
            if (lunchStart.HasValue && lunchEnd.HasValue)
            {
                if (slotStart < lunchEnd.Value && slotEnd > lunchStart.Value)
                {
                    current = slotEnd;
                    continue;
                }
            }

            // Excluir festivos parciales
            bool inPartialHoliday = false;
            foreach (var ph in partialHolidays)
            {
                if (ph.Date.Date == date.Date && ph.StartTime.HasValue && ph.EndTime.HasValue)
                {
                    var phStart = ph.StartTime.Value.ToTimeSpan();
                    var phEnd = ph.EndTime.Value.ToTimeSpan();
                    if (slotStart < phEnd && slotEnd > phStart)
                    {
                        inPartialHoliday = true;
                        break;
                    }
                }
            }
            if (inPartialHoliday)
            {
                current = slotEnd;
                continue;
            }

            var slot = new TimeSlot
            {
                Id = Guid.NewGuid(),
                ClinicId = schedule.ClinicId,
                DoctorId = schedule.DoctorId,
                StartAt = date.Date + slotStart,
                EndAt = date.Date + slotEnd,
                IsBooked = false,
                IsBlocked = false,
                CreatedAt = DateTime.UtcNow
            };
            slots.Add(slot);

            current = slotEnd;
        }

        return slots;
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Extension para registrar el servicio en DI
/// </summary>
public static class SlotGeneratorServiceExtensions
{
    public static IServiceCollection AddSlotGenerator(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SlotGeneratorOptions>(configuration.GetSection(SlotGeneratorOptions.SectionName));
        services.AddHostedService<SlotGeneratorService>();
        return services;
    }
}