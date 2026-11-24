using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klinara.Web.Services;

public interface IAppointmentService
{
    Task<AppointmentResult> CreateAppointmentAsync(Appointment appointment, bool isPatientBooking = false);
    Task<AppointmentResult> UpdateAppointmentAsync(Appointment appointment, bool isPatientBooking = false);
    Task<AppointmentResult> CancelAppointmentAsync(Guid appointmentId, Guid cancelledByUserId, string reason, bool isPatientCancellation = false);
    Task<AppointmentResult> MarkNoShowAsync(Guid appointmentId, Guid markedByUserId);
    Task<AppointmentResult> UnblockPatientAsync(Guid patientId, Guid unblockedByUserId);
    Task<Appointment?> GetAppointmentAsync(Guid appointmentId);
    Task<List<Appointment>> GetAppointmentsByClinicAsync(Guid clinicId, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<Appointment>> GetAppointmentsByDoctorAsync(Guid doctorId, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<Appointment>> GetAppointmentsByPatientAsync(Guid patientId, DateTime? startDate = null, DateTime? endDate = null);
}

public class AppointmentResult
{
    public bool Success { get; set; }
    public Appointment? Appointment { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class AppointmentService : IAppointmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IScheduleService _scheduleService;
    private readonly ILogger<AppointmentService> _logger;

    public AppointmentService(ApplicationDbContext context, IScheduleService scheduleService, ILogger<AppointmentService> logger)
    {
        _context = context;
        _scheduleService = scheduleService;
        _logger = logger;
    }

    public async Task<AppointmentResult> CreateAppointmentAsync(Appointment appointment, bool isPatientBooking = false)
    {
        var result = new AppointmentResult();

        // Validar usando ScheduleService
        var validation = await _scheduleService.ValidateAppointmentAsync(appointment, isPatientBooking);
        if (!validation.IsValid)
        {
            result.Success = false;
            result.Errors = validation.Errors;
            return result;
        }

        try
        {
            appointment.Id = Guid.NewGuid();
            appointment.CreatedAt = DateTime.UtcNow;
            appointment.Status = AppointmentStatus.Scheduled;

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            // Generar TimeSlot para esta cita
            await GenerateTimeSlotForAppointmentAsync(appointment);

            result.Success = true;
            result.Appointment = appointment;
            _logger.LogInformation("Cita creada: {AppointmentId} para Doctor {DoctorId} Paciente {PatientId}", appointment.Id, appointment.DoctorId, appointment.PatientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando cita");
            result.Success = false;
            result.Errors.Add("Error interno al crear la cita");
        }

        return result;
    }

    public async Task<AppointmentResult> UpdateAppointmentAsync(Appointment appointment, bool isPatientBooking = false)
    {
        var result = new AppointmentResult();

        var existing = await _context.Appointments.FindAsync(appointment.Id);
        if (existing == null)
        {
            result.Success = false;
            result.Errors.Add("Cita no encontrada");
            return result;
        }

        // Validar cambios si hay cambio de horario
        if (existing.StartAt != appointment.StartAt || existing.EndAt != appointment.EndAt || existing.DoctorId != appointment.DoctorId)
        {
            var validationAppointment = new Appointment
            {
                Id = appointment.Id,
                ClinicId = appointment.ClinicId,
                DoctorId = appointment.DoctorId,
                PatientId = appointment.PatientId,
                StartAt = appointment.StartAt,
                EndAt = appointment.EndAt
            };

            var validation = await _scheduleService.ValidateAppointmentAsync(validationAppointment, isPatientBooking);
            if (!validation.IsValid)
            {
                result.Success = false;
                result.Errors = validation.Errors;
                return result;
            }
        }

        try
        {
            // Guardar estado anterior para historial
            var previousStatus = existing.Status;
            var previousStartAt = existing.StartAt;
            var previousEndAt = existing.EndAt;
            var previousDoctorId = existing.DoctorId;

            existing.StartAt = appointment.StartAt;
            existing.EndAt = appointment.EndAt;
            existing.DoctorId = appointment.DoctorId;
            existing.Notes = appointment.Notes;
            existing.Type = appointment.Type;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Registrar en historial si cambió algo relevante
            if (previousStartAt != appointment.StartAt || previousEndAt != appointment.EndAt || previousDoctorId != appointment.DoctorId)
            {
                _context.AppointmentHistory.Add(new AppointmentHistory
                {
                    AppointmentId = appointment.Id,
                    FromStatus = previousStatus,
                    ToStatus = appointment.Status,
                    Reason = "Reprogramación de cita",
                    ChangedByUserId = appointment.CreatedByUserId,
                    At = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            // Regenerar TimeSlot si cambió horario
            await RegenerateTimeSlotForAppointmentAsync(appointment);

            result.Success = true;
            result.Appointment = existing;
            _logger.LogInformation("Cita actualizada: {AppointmentId}", appointment.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando cita {AppointmentId}", appointment.Id);
            result.Success = false;
            result.Errors.Add("Error interno al actualizar la cita");
        }

        return result;
    }

    public async Task<AppointmentResult> CancelAppointmentAsync(Guid appointmentId, Guid cancelledByUserId, string reason, bool isPatientCancellation = false)
    {
        var result = new AppointmentResult();

        var appointment = await _context.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null)
        {
            result.Success = false;
            result.Errors.Add("Cita no encontrada");
            return result;
        }

        if (appointment.Status == AppointmentStatus.CancelledByPatient || appointment.Status == AppointmentStatus.CancelledByStaff)
        {
            result.Success = false;
            result.Errors.Add("La cita ya está cancelada");
            return result;
        }

        try
        {
            var previousStatus = appointment.Status;
            var isLateCancellation = false;

            // Verificar si es cancelación tardía (< 24h)
            var hoursUntilAppointment = (appointment.StartAt - DateTime.UtcNow).TotalHours;
            if (hoursUntilAppointment < 24 && hoursUntilAppointment > 0)
            {
                isLateCancellation = true;
            }

            appointment.Status = isPatientCancellation ? AppointmentStatus.CancelledByPatient : AppointmentStatus.CancelledByStaff;
            appointment.CancellationReason = reason;
            appointment.CancelledAt = DateTime.UtcNow;
            appointment.CancelledByUserId = cancelledByUserId;
            appointment.IsLateCancellation = isLateCancellation;
            appointment.UpdatedAt = DateTime.UtcNow;

            // Registrar en historial
            _context.AppointmentHistory.Add(new AppointmentHistory
            {
                AppointmentId = appointment.Id,
                FromStatus = appointment.Status == AppointmentStatus.CancelledByPatient ? AppointmentStatus.CancelledByPatient : AppointmentStatus.CancelledByStaff,
                ToStatus = appointment.Status,
                Reason = $"Cancelación {(isPatientCancellation ? "por paciente" : "por personal")}: {reason} {(isLateCancellation ? "(tardía)" : "")}",
                ChangedByUserId = cancelledByUserId,
                At = DateTime.UtcNow
            });

            // Si es cancelación tardía por paciente, incrementar contador de impuntualidad
            if (isLateCancellation && !isPatientCancellation)
            {
                // Solo cuenta si el paciente la cancela, no si la cancela el personal
            }
            else if (isLateCancellation && isPatientCancellation)
            {
                var patient = appointment.Patient;
                if (patient != null)
                {
                    patient.LateCancellationCount++;
                    if (patient.LateCancellationCount >= 3)
                    {
                        patient.IsBlocked = true;
                        patient.BlockedUntil = DateTime.UtcNow.AddMonths(3); // Bloqueo 3 meses
                    }
                }
            }

            // Liberar TimeSlot
            await ReleaseTimeSlotForAppointmentAsync(appointment.Id);

            await _context.SaveChangesAsync();

            result.Success = true;
            result.Appointment = appointment;
            _logger.LogInformation("Cita cancelada: {AppointmentId}, Tardía: {IsLate}, Por: {UserId}", appointmentId, isLateCancellation, cancelledByUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelando cita {AppointmentId}", appointmentId);
            result.Success = false;
            result.Errors.Add("Error interno al cancelar la cita");
        }

        return result;
    }

    public async Task<AppointmentResult> MarkNoShowAsync(Guid appointmentId, Guid markedByUserId)
    {
        var result = new AppointmentResult();

        var appointment = await _context.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null)
        {
            result.Success = false;
            result.Errors.Add("Cita no encontrada");
            return result;
        }

        if (appointment.Status != AppointmentStatus.Scheduled && appointment.Status != AppointmentStatus.Confirmed)
        {
            result.Success = false;
            result.Errors.Add("Solo se puede marcar no-show a citas programadas o confirmadas");
            return result;
        }

        try
        {
            appointment.Status = AppointmentStatus.NoShow;
            appointment.IsNoShow = true;
            appointment.UpdatedAt = DateTime.UtcNow;

            // Registrar en historial
            _context.AppointmentHistory.Add(new AppointmentHistory
            {
                AppointmentId = appointment.Id,
                FromStatus = AppointmentStatus.Scheduled,
                ToStatus = AppointmentStatus.NoShow,
                Reason = "No-show marcado por personal",
                ChangedByUserId = markedByUserId,
                At = DateTime.UtcNow
            });

            // Incrementar contador de no-show del paciente
            var patient = appointment.Patient;
            if (patient != null)
            {
                patient.NoShowCount++;
            }

            // Liberar TimeSlot
            await ReleaseTimeSlotForAppointmentAsync(appointment.Id);

            await _context.SaveChangesAsync();

            result.Success = true;
            result.Appointment = appointment;
            _logger.LogInformation("No-show marcado: {AppointmentId} por {UserId}", appointmentId, markedByUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marcando no-show {AppointmentId}", appointmentId);
            result.Success = false;
            result.Errors.Add("Error interno al marcar no-show");
        }

        return result;
    }

    public async Task<AppointmentResult> UnblockPatientAsync(Guid patientId, Guid unblockedByUserId)
    {
        var result = new AppointmentResult();

        var patient = await _context.Patients.FindAsync(patientId);
        if (patient == null)
        {
            result.Success = false;
            result.Errors.Add("Paciente no encontrado");
            return result;
        }

        if (!patient.IsBlocked)
        {
            result.Success = false;
            result.Errors.Add("El paciente no está bloqueado");
            return result;
        }

        try
        {
            patient.IsBlocked = false;
            patient.BlockedUntil = null;
            patient.LateCancellationCount = 0; // Resetear contador
            patient.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            result.Success = true;
            _logger.LogInformation("Paciente desbloqueado: {PatientId} por {UserId}", patientId, unblockedByUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error desbloqueando paciente {PatientId}", patientId);
            result.Success = false;
            result.Errors.Add("Error interno al desbloquear paciente");
        }

        return result;
    }

    public async Task<Appointment?> GetAppointmentAsync(Guid appointmentId)
    {
        return await _context.Appointments
            .Include(a => a.Doctor)
            .Include(a => a.Patient)
            .Include(a => a.CreatedByUser)
            .Include(a => a.CancelledByUser)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);
    }

    public async Task<List<Appointment>> GetAppointmentsByClinicAsync(Guid clinicId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Appointments
            .Where(a => a.ClinicId == clinicId);

        if (startDate.HasValue)
            query = query.Where(a => a.StartAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.StartAt <= endDate.Value);

        return await query
            .Include(a => a.Doctor)
            .Include(a => a.Patient)
            .OrderBy(a => a.StartAt)
            .ToListAsync();
    }

    public async Task<List<Appointment>> GetAppointmentsByDoctorAsync(Guid doctorId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Appointments
            .Where(a => a.DoctorId == doctorId);

        if (startDate.HasValue)
            query = query.Where(a => a.StartAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.StartAt <= endDate.Value);

        return await query
            .Include(a => a.Patient)
            .OrderBy(a => a.StartAt)
            .ToListAsync();
    }

    public async Task<List<Appointment>> GetAppointmentsByPatientAsync(Guid patientId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Appointments
            .Where(a => a.PatientId == patientId);

        if (startDate.HasValue)
            query = query.Where(a => a.StartAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.StartAt <= endDate.Value);

        return await query
            .Include(a => a.Doctor)
            .OrderBy(a => a.StartAt)
            .ToListAsync();
    }

    // Métodos privados auxiliares

    private async Task GenerateTimeSlotForAppointmentAsync(Appointment appointment)
    {
        var existingSlot = await _context.TimeSlots
            .FirstOrDefaultAsync(s => s.ClinicId == appointment.ClinicId
                && s.DoctorId == appointment.DoctorId
                && s.StartAt == appointment.StartAt
                && s.EndAt == appointment.EndAt);

        if (existingSlot == null)
        {
            var timeSlot = new TimeSlot
            {
                Id = Guid.NewGuid(),
                ClinicId = appointment.ClinicId,
                DoctorId = appointment.DoctorId,
                StartAt = appointment.StartAt,
                EndAt = appointment.EndAt,
                IsBooked = true,
                AppointmentId = appointment.Id,
                IsBlocked = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.TimeSlots.Add(timeSlot);
        }
        else
        {
            existingSlot.IsBooked = true;
            existingSlot.AppointmentId = appointment.Id;
            existingSlot.IsBlocked = false;
        }

        await _context.SaveChangesAsync();
    }

    private async Task RegenerateTimeSlotForAppointmentAsync(Appointment appointment)
    {
        // Liberar slot anterior si existía con diferente horario
        var oldSlots = await _context.TimeSlots
            .Where(s => s.AppointmentId == appointment.Id)
            .ToListAsync();

        foreach (var slot in oldSlots)
        {
            slot.IsBooked = false;
            slot.AppointmentId = null;
        }

        await GenerateTimeSlotForAppointmentAsync(appointment);
    }

    private async Task ReleaseTimeSlotForAppointmentAsync(Guid appointmentId)
    {
        var slots = await _context.TimeSlots
            .Where(s => s.AppointmentId == appointmentId)
            .ToListAsync();

        foreach (var slot in slots)
        {
            slot.IsBooked = false;
            slot.AppointmentId = null;
        }

        await _context.SaveChangesAsync();
    }
}