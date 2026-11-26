using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klinara.Web.Services;

public interface IScheduleService
{
    Task<ScheduleValidationResult> ValidateAppointmentAsync(Appointment appointment, bool isPatientBooking = false);
    Task<List<TimeSlot>> GenerateSlotsForPeriodAsync(Guid clinicId, Guid doctorId, DateTime startDate, DateTime endDate);
    Task<bool> IsSlotAvailableAsync(Guid clinicId, Guid doctorId, DateTime startAt, DateTime endAt, Guid? excludeAppointmentId = null);
    Task<List<DoctorSchedule>> GetDoctorSchedulesAsync(Guid clinicId, Guid doctorId, DateTime? effectiveAt = null);
    Task<bool> IsClinicHolidayAsync(Guid clinicId, DateTime date);
    Task<bool> IsDoctorOnVacationAsync(Guid doctorId, DateTime date);
}

public class ScheduleValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ScheduleService : IScheduleService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ScheduleService> _logger;

    // Constantes del PRD
    private const int PatientBookingHorizonDays = 45;
    private const int MinAdvanceBookingHours = 24;

    public ScheduleService(ApplicationDbContext context, ILogger<ScheduleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ScheduleValidationResult> ValidateAppointmentAsync(Appointment appointment, bool isPatientBooking = false)
    {
        var result = new ScheduleValidationResult();

        // 1. Validar que la clínica existe y está activa
        var clinic = await _context.Clinics.FindAsync(appointment.ClinicId);
        if (clinic == null || !clinic.IsActive)
        {
            result.Errors.Add("La clínica no existe o está inactiva");
            return result;
        }

        // Obtener zona horaria de la clínica
        TimeZoneInfo clinicTimeZone;
        try
        {
            clinicTimeZone = clinic.TimeZone;
        }
        catch (System.TimeZoneNotFoundException)
        {
            clinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
        }

        // Convertir la hora de la cita a la zona horaria de la clínica para validaciones
        // Si la cita viene con Kind=Unspecified, asumimos que ya está en hora local de la clínica
        // Si viene con Kind=Utc, convertimos de UTC a hora local de la clínica
        var appointmentLocalStart = appointment.StartAt.Kind == DateTimeKind.Utc
            ? TimeZoneInfo.ConvertTimeFromUtc(appointment.StartAt, clinicTimeZone)
            : DateTime.SpecifyKind(appointment.StartAt, DateTimeKind.Unspecified);
        var appointmentLocalEnd = appointment.EndAt.Kind == DateTimeKind.Utc
            ? TimeZoneInfo.ConvertTimeFromUtc(appointment.EndAt, clinicTimeZone)
            : DateTime.SpecifyKind(appointment.EndAt, DateTimeKind.Unspecified);

        // 2. Validar que el doctor pertenece a la clínica y está activo
        var doctor = await _context.Users.FindAsync(appointment.DoctorId);
        if (doctor == null || doctor.ClinicId != appointment.ClinicId)
        {
            result.Errors.Add("El doctor no pertenece a esta clínica");
            return result;
        }

        // 3. Validar que el paciente pertenece a la clínica
        var patient = await _context.Patients.FindAsync(appointment.PatientId);
        if (patient == null || patient.ClinicId != appointment.ClinicId)
        {
            result.Errors.Add("El paciente no pertenece a esta clínica");
            return result;
        }

        // 4. Validar horario: StartAt < EndAt
        if (appointment.StartAt >= appointment.EndAt)
        {
            result.Errors.Add("La hora de inicio debe ser anterior a la hora de fin");
            return result;
        }

        // 5. Validar duración mínima (ej. 15 min) y que coincida con slot duration del doctor
        var durationMinutes = (int)(appointment.EndAt - appointment.StartAt).TotalMinutes;
        if (durationMinutes < 15)
        {
            result.Errors.Add("La duración mínima de una cita es 15 minutos");
            return result;
        }

        // 6. Validar traslapes con otras citas del mismo doctor (usar hora UTC original para BD)
        var hasOverlap = await _context.Appointments
            .Where(a => a.DoctorId == appointment.DoctorId
                && a.ClinicId == appointment.ClinicId
                && a.Id != appointment.Id
                && a.Status != AppointmentStatus.CancelledByPatient
                && a.Status != AppointmentStatus.CancelledByStaff
                && a.StartAt < appointment.EndAt
                && a.EndAt > appointment.StartAt)
            .AnyAsync();

        if (hasOverlap)
        {
            result.Errors.Add("El doctor ya tiene una cita en ese horario (traslape detectado)");
            return result;
        }

        // 7. Validar horario dentro de DoctorSchedule (usar hora local de clínica)
        var doctorSchedules = await GetDoctorSchedulesAsync(appointment.ClinicId, appointment.DoctorId, appointment.StartAt);
        var isWithinSchedule = doctorSchedules.Any(ds =>
            ds.DayOfWeek == appointmentLocalStart.DayOfWeek &&
            TimeOnly.FromDateTime(appointmentLocalStart) >= ds.StartTime &&
            TimeOnly.FromDateTime(appointmentLocalEnd) <= ds.EndTime);

        if (!isWithinSchedule)
        {
            result.Errors.Add("La cita está fuera del horario laboral del doctor");
            return result;
        }

        // 8. Validar que no caiga en intervalo de comida
        var isLunchTime = doctorSchedules.Any(ds =>
            ds.DayOfWeek == appointmentLocalStart.DayOfWeek &&
            ds.LunchStart.HasValue && ds.LunchEnd.HasValue &&
            TimeOnly.FromDateTime(appointmentLocalStart) < ds.LunchEnd.Value &&
            TimeOnly.FromDateTime(appointmentLocalEnd) > ds.LunchStart.Value);

        if (isLunchTime)
        {
            result.Errors.Add("La cita cae dentro del intervalo de comida del doctor");
            return result;
        }

        // 9. Validar festivos de clínica (usar fecha local)
        if (await IsClinicHolidayAsync(appointment.ClinicId, appointmentLocalStart))
        {
            result.Errors.Add("La clínica está cerrada por festivo en esa fecha");
            return result;
        }

        // 10. Validar vacaciones del doctor (usar fecha local)
        if (await IsDoctorOnVacationAsync(appointment.DoctorId, appointmentLocalStart))
        {
            result.Errors.Add("El doctor está de vacaciones en esa fecha");
            return result;
        }

        // 11. Validar horizonte de reserva (solo para pacientes)
        if (isPatientBooking)
        {
            var now = DateTime.UtcNow;
            var minBookingTime = now.AddHours(MinAdvanceBookingHours);
            var maxBookingTime = now.AddDays(PatientBookingHorizonDays);

            if (appointment.StartAt < minBookingTime)
            {
                result.Errors.Add($"Las citas deben reservarse con al menos {MinAdvanceBookingHours} horas de antelación");
                return result;
            }

            if (appointment.StartAt > maxBookingTime)
            {
                result.Errors.Add($"Los pacientes solo pueden reservar con hasta {PatientBookingHorizonDays} días de antelación");
                return result;
            }
        }

        // 12. Validar que el slot esté disponible
        var slotAvailable = await IsSlotAvailableAsync(appointment.ClinicId, appointment.DoctorId, appointment.StartAt, appointment.EndAt, appointment.Id);
        if (!slotAvailable)
        {
            result.Errors.Add("El horario ya no está disponible");
            return result;
        }

        // 13. Validar bloqueo de paciente (3 impuntualidades)
        var patientEntity = await _context.Patients.FindAsync(appointment.PatientId);
        if (patientEntity != null && patientEntity.IsBlocked && patientEntity.BlockedUntil.HasValue && patientEntity.BlockedUntil.Value > DateTime.UtcNow)
        {
            result.Errors.Add("El paciente está bloqueado para reservas por impuntualidades repetidas");
            return result;
        }

        result.IsValid = true;
        return result;
    }

    public async Task<List<TimeSlot>> GenerateSlotsForPeriodAsync(Guid clinicId, Guid doctorId, DateTime startDate, DateTime endDate)
    {
        var slots = new List<TimeSlot>();

        // Obtener zona horaria de la clínica
        var clinic = await _context.Clinics.FindAsync(clinicId);
        TimeZoneInfo clinicTimeZone;
        try
        {
            clinicTimeZone = clinic?.TimeZone ?? TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
        }
        catch (System.TimeZoneNotFoundException)
        {
            clinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
        }

        // Convertir fechas UTC a hora local de la clínica para determinar qué días generar
        var startDateLocal = startDate.Kind == DateTimeKind.Utc
            ? TimeZoneInfo.ConvertTimeFromUtc(startDate, clinicTimeZone)
            : DateTime.SpecifyKind(startDate, DateTimeKind.Unspecified);
        var endDateLocal = endDate.Kind == DateTimeKind.Utc
            ? TimeZoneInfo.ConvertTimeFromUtc(endDate, clinicTimeZone)
            : DateTime.SpecifyKind(endDate, DateTimeKind.Unspecified);

        // Obtener horarios del doctor vigentes en el período
        var schedules = await _context.DoctorSchedules
            .Where(ds => ds.ClinicId == clinicId
                && ds.DoctorId == doctorId
                && ds.IsActive
                && ds.EffectiveFrom <= endDateLocal.Date
                && (ds.EffectiveTo == null || ds.EffectiveTo >= startDateLocal.Date))
            .ToListAsync();

        if (!schedules.Any())
        {
            _logger.LogWarning("No hay horarios configurados para doctor {DoctorId} en clínica {ClinicId}", doctorId, clinicId);
            return slots;
        }

        // Obtener festivos y vacaciones en el período (usar fechas locales)
        var holidays = await _context.ClinicHolidays
            .Where(h => h.ClinicId == clinicId && h.Date.Date >= startDateLocal.Date && h.Date.Date <= endDateLocal.Date)
            .ToListAsync();

        var vacations = await _context.DoctorVacations
            .Where(v => v.DoctorId == doctorId && v.StartDate.Date <= endDateLocal.Date && v.EndDate.Date >= startDateLocal.Date)
            .ToListAsync();

        // Obtener citas existentes en el período (usar UTC para BD)
        var existingAppointments = await _context.Appointments
            .Where(a => a.DoctorId == doctorId
                && a.ClinicId == clinicId
                && a.StartAt >= startDate
                && a.StartAt <= endDate
                && a.Status != AppointmentStatus.CancelledByPatient
                && a.Status != AppointmentStatus.CancelledByStaff)
            .Select(a => new { a.StartAt, a.EndAt })
            .ToListAsync();

        // Obtener slots ya generados en el período (usar UTC para BD)
        var existingSlots = await _context.TimeSlots
            .Where(s => s.DoctorId == doctorId && s.ClinicId == clinicId && s.StartAt >= startDate && s.StartAt <= endDate)
            .ToListAsync();

        var currentDateLocal = startDateLocal.Date;
        while (currentDateLocal <= endDateLocal.Date)
        {
            // Saltar festivos de clínica
            if (holidays.Any(h => h.Date.Date == currentDateLocal.Date && (h.IsFullDay || (h.StartTime.HasValue && h.EndTime.HasValue))))
            {
                currentDateLocal = currentDateLocal.AddDays(1);
                continue;
            }

            // Saltar vacaciones del doctor
            if (vacations.Any(v => currentDateLocal.Date >= v.StartDate.Date && currentDateLocal.Date <= v.EndDate.Date))
            {
                currentDateLocal = currentDateLocal.AddDays(1);
                continue;
            }

            // Buscar horario para este día de la semana (en hora local de clínica)
            var daySchedules = schedules.Where(s => s.DayOfWeek == currentDateLocal.DayOfWeek).ToList();
            if (!daySchedules.Any())
            {
                currentDateLocal = currentDateLocal.AddDays(1);
                continue;
            }

            foreach (var schedule in daySchedules)
            {
                var slotDuration = TimeSpan.FromMinutes(schedule.SlotDurationMinutes);
                var currentTimeLocal = currentDateLocal.Add(schedule.StartTime.ToTimeSpan());

                while (currentTimeLocal.Add(slotDuration) <= currentDateLocal.Add(schedule.EndTime.ToTimeSpan()))
                {
                    var slotEndLocal = currentTimeLocal.Add(slotDuration);

                    // Saltar intervalo de comida
                    if (schedule.LunchStart.HasValue && schedule.LunchEnd.HasValue)
                    {
                        var lunchStartTs = schedule.LunchStart.Value.ToTimeSpan();
                        var lunchEndTs = schedule.LunchEnd.Value.ToTimeSpan();
                        var currentTs = currentTimeLocal.TimeOfDay;
                        var slotEndTs = slotEndLocal.TimeOfDay;

                        if (currentTs < lunchEndTs && slotEndTs > lunchStartTs)
                        {
                            currentTimeLocal = slotEndLocal;
                            continue;
                        }
                    }

                    // Convertir horas locales a UTC para almacenamiento y comparación
                    var slotStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(currentTimeLocal, DateTimeKind.Unspecified), clinicTimeZone);
                    var slotEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(slotEndLocal, DateTimeKind.Unspecified), clinicTimeZone);

                    // Verificar si ya existe slot (comparar en UTC)
                    var existingSlot = existingSlots.FirstOrDefault(s => s.StartAt == slotStartUtc && s.EndAt == slotEndUtc);
                    if (existingSlot != null)
                    {
                        currentTimeLocal = slotEndLocal;
                        continue;
                    }

                    // Verificar si hay cita en este slot (comparar en UTC)
                    var hasAppointment = existingAppointments.Any(a =>
                        a.StartAt < slotEndUtc && a.EndAt > slotStartUtc);

                    var newSlot = new TimeSlot
                    {
                        Id = Guid.NewGuid(),
                        ClinicId = clinicId,
                        DoctorId = doctorId,
                        StartAt = slotStartUtc,
                        EndAt = slotEndUtc,
                        IsBooked = hasAppointment,
                        IsBlocked = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    if (hasAppointment)
                    {
                        var appointment = existingAppointments.FirstOrDefault(a => a.StartAt == slotStartUtc && a.EndAt == slotEndUtc);
                        if (appointment != null)
                        {
                            newSlot.AppointmentId = _context.Appointments
                                .FirstOrDefault(ap => ap.StartAt == slotStartUtc && ap.EndAt == slotEndUtc)?.Id;
                        }
                    }

                    slots.Add(newSlot);
                    currentTimeLocal = slotEndLocal;
                }
            }

            currentDateLocal = currentDateLocal.AddDays(1);
        }

        return slots;
    }

    public async Task<bool> IsSlotAvailableAsync(Guid clinicId, Guid doctorId, DateTime startAt, DateTime endAt, Guid? excludeAppointmentId = null)
    {
        // Verificar traslapes con citas existentes
        var hasOverlap = await _context.Appointments
            .Where(a => a.DoctorId == doctorId
                && a.ClinicId == clinicId
                && (excludeAppointmentId == null || a.Id != excludeAppointmentId)
                && a.Status != AppointmentStatus.CancelledByPatient
                && a.Status != AppointmentStatus.CancelledByStaff
                && a.StartAt < endAt
                && a.EndAt > startAt)
            .AnyAsync();

        if (hasOverlap) return false;

        // Verificar si el slot está bloqueado
        var blockedSlot = await _context.TimeSlots
            .Where(s => s.ClinicId == clinicId
                && s.DoctorId == doctorId
                && s.StartAt < endAt
                && s.EndAt > startAt
                && s.IsBlocked)
            .AnyAsync();

        return !blockedSlot;
    }

    public async Task<List<DoctorSchedule>> GetDoctorSchedulesAsync(Guid clinicId, Guid doctorId, DateTime? effectiveAt = null)
    {
        var query = _context.DoctorSchedules
            .Where(ds => ds.ClinicId == clinicId && ds.DoctorId == doctorId && ds.IsActive);

        if (effectiveAt.HasValue)
        {
            // effectiveAt viene en UTC o local; obtener fecha local de la clínica
            var clinic = await _context.Clinics.FindAsync(clinicId);
            TimeZoneInfo clinicTimeZone;
            try
            {
                clinicTimeZone = clinic?.TimeZone ?? TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
            }
            catch (System.TimeZoneNotFoundException)
            {
                clinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
            }

            var effectiveAtLocal = effectiveAt.Value.Kind == DateTimeKind.Utc
                ? TimeZoneInfo.ConvertTimeFromUtc(effectiveAt.Value, clinicTimeZone)
                : DateTime.SpecifyKind(effectiveAt.Value, DateTimeKind.Unspecified);
            var effectiveAtDate = effectiveAtLocal.Date;

            query = query.Where(ds => ds.EffectiveFrom <= effectiveAtDate && (ds.EffectiveTo == null || ds.EffectiveTo >= effectiveAtDate));
        }

        return await query.OrderBy(ds => ds.DayOfWeek).ThenBy(ds => ds.StartTime).ToListAsync();
    }

    public async Task<bool> IsClinicHolidayAsync(Guid clinicId, DateTime date)
    {
        return await _context.ClinicHolidays
            .AnyAsync(h => h.ClinicId == clinicId && h.Date.Date == date.Date && (h.IsFullDay || (h.StartTime.HasValue && h.EndTime.HasValue)));
    }

    public async Task<bool> IsDoctorOnVacationAsync(Guid doctorId, DateTime date)
    {
        return await _context.DoctorVacations
            .AnyAsync(v => v.DoctorId == doctorId && date.Date >= v.StartDate.Date && date.Date <= v.EndDate.Date);
    }
}