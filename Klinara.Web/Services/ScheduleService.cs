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

        // 6. Validar traslapes con otras citas del mismo doctor
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

        // 7. Validar horario dentro de DoctorSchedule
        var doctorSchedules = await GetDoctorSchedulesAsync(appointment.ClinicId, appointment.DoctorId, appointment.StartAt);
        var isWithinSchedule = doctorSchedules.Any(ds =>
            ds.DayOfWeek == appointment.StartAt.DayOfWeek &&
            TimeOnly.FromDateTime(appointment.StartAt) >= ds.StartTime &&
            TimeOnly.FromDateTime(appointment.EndAt) <= ds.EndTime);

        if (!isWithinSchedule)
        {
            result.Errors.Add("La cita está fuera del horario laboral del doctor");
            return result;
        }

        // 8. Validar que no caiga en intervalo de comida
        var isLunchTime = doctorSchedules.Any(ds =>
            ds.DayOfWeek == appointment.StartAt.DayOfWeek &&
            ds.LunchStart.HasValue && ds.LunchEnd.HasValue &&
            TimeOnly.FromDateTime(appointment.StartAt) < ds.LunchEnd.Value &&
            TimeOnly.FromDateTime(appointment.EndAt) > ds.LunchStart.Value);

        if (isLunchTime)
        {
            result.Errors.Add("La cita cae dentro del intervalo de comida del doctor");
            return result;
        }

        // 9. Validar festivos de clínica
        if (await IsClinicHolidayAsync(appointment.ClinicId, appointment.StartAt))
        {
            result.Errors.Add("La clínica está cerrada por festivo en esa fecha");
            return result;
        }

        // 10. Validar vacaciones del doctor
        if (await IsDoctorOnVacationAsync(appointment.DoctorId, appointment.StartAt))
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

        // Obtener horarios del doctor vigentes en el período
        var schedules = await _context.DoctorSchedules
            .Where(ds => ds.ClinicId == clinicId
                && ds.DoctorId == doctorId
                && ds.IsActive
                && ds.EffectiveFrom <= endDate
                && (ds.EffectiveTo == null || ds.EffectiveTo >= startDate))
            .ToListAsync();

        if (!schedules.Any())
        {
            _logger.LogWarning("No hay horarios configurados para doctor {DoctorId} en clínica {ClinicId}", doctorId, clinicId);
            return slots;
        }

        // Obtener festivos y vacaciones en el período
        var holidays = await _context.ClinicHolidays
            .Where(h => h.ClinicId == clinicId && h.Date >= startDate && h.Date <= endDate)
            .ToListAsync();

        var vacations = await _context.DoctorVacations
            .Where(v => v.DoctorId == doctorId && v.StartDate <= endDate && v.EndDate >= startDate)
            .ToListAsync();

        // Obtener citas existentes en el período (para marcar slots ocupados)
        var existingAppointments = await _context.Appointments
            .Where(a => a.DoctorId == doctorId
                && a.ClinicId == clinicId
                && a.StartAt >= startDate
                && a.StartAt <= endDate
                && a.Status != AppointmentStatus.CancelledByPatient
                && a.Status != AppointmentStatus.CancelledByStaff)
            .Select(a => new { a.StartAt, a.EndAt })
            .ToListAsync();

        // Obtener slots ya generados en el período
        var existingSlots = await _context.TimeSlots
            .Where(s => s.DoctorId == doctorId && s.ClinicId == clinicId && s.StartAt >= startDate && s.StartAt <= endDate)
            .ToListAsync();

        var currentDate = startDate.Date;
        while (currentDate <= endDate.Date)
        {
            // Saltar festivos de clínica
            if (holidays.Any(h => h.Date.Date == currentDate.Date && (h.IsFullDay || (h.StartTime.HasValue && h.EndTime.HasValue))))
            {
                currentDate = currentDate.AddDays(1);
                continue;
            }

            // Saltar vacaciones del doctor
            if (vacations.Any(v => currentDate.Date >= v.StartDate.Date && currentDate.Date <= v.EndDate.Date))
            {
                currentDate = currentDate.AddDays(1);
                continue;
            }

            // Buscar horario para este día de la semana
            var daySchedules = schedules.Where(s => s.DayOfWeek == currentDate.DayOfWeek).ToList();
            if (!daySchedules.Any())
            {
                currentDate = currentDate.AddDays(1);
                continue;
            }

            foreach (var schedule in daySchedules)
            {
                var slotDuration = TimeSpan.FromMinutes(schedule.SlotDurationMinutes);
                var currentTime = currentDate.Add(schedule.StartTime.ToTimeSpan());

                while (currentTime.Add(slotDuration) <= currentDate.Add(schedule.EndTime.ToTimeSpan()))
                {
                    var slotEnd = currentTime.Add(slotDuration);

                    // Saltar intervalo de comida
                    if (schedule.LunchStart.HasValue && schedule.LunchEnd.HasValue)
                    {
                        var lunchStartTs = schedule.LunchStart.Value.ToTimeSpan();
                        var lunchEndTs = schedule.LunchEnd.Value.ToTimeSpan();
                        var currentTs = currentTime.TimeOfDay;
                        var slotEndTs = slotEnd.TimeOfDay;

                        if (currentTs < lunchEndTs && slotEndTs > lunchStartTs)
                        {
                            currentTime = slotEnd;
                            continue;
                        }
                    }

                    // Verificar si ya existe slot
                    var existingSlot = existingSlots.FirstOrDefault(s => s.StartAt == currentTime && s.EndAt == slotEnd);
                    if (existingSlot != null)
                    {
                        currentTime = slotEnd;
                        continue;
                    }

                    // Verificar si hay cita en este slot
                    var hasAppointment = existingAppointments.Any(a =>
                        a.StartAt < slotEnd && a.EndAt > currentTime);

                    var newSlot = new TimeSlot
                    {
                        Id = Guid.NewGuid(),
                        ClinicId = clinicId,
                        DoctorId = doctorId,
                        StartAt = currentTime,
                        EndAt = slotEnd,
                        IsBooked = hasAppointment,
                        IsBlocked = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    if (hasAppointment)
                    {
                        var appointment = existingAppointments.First(a => a.StartAt == currentTime && a.EndAt == slotEnd);
                        newSlot.AppointmentId = appointment.StartAt == currentTime && appointment.EndAt == slotEnd
                            ? _context.Appointments.First(ap => ap.StartAt == currentTime && ap.EndAt == slotEnd).Id
                            : null;
                    }

                    slots.Add(newSlot);
                    currentTime = slotEnd;
                }
            }

            currentDate = currentDate.AddDays(1);
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
            query = query.Where(ds => ds.EffectiveFrom <= effectiveAt.Value && (ds.EffectiveTo == null || ds.EffectiveTo >= effectiveAt.Value));
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