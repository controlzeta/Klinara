using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Klinara.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private readonly IScheduleService _scheduleService;
    private readonly ApplicationDbContext _context;

    public SchedulesController(IScheduleService scheduleService, ApplicationDbContext context)
    {
        _scheduleService = scheduleService;
        _context = context;
    }

    // GET: api/schedules/clinic/{clinicId}/doctor/{doctorId}
    [HttpGet("clinic/{clinicId:guid}/doctor/{doctorId:guid}")]
    [Authorize(Policy = "CanManageSchedules")]
    public async Task<ActionResult<List<DoctorSchedule>>> GetSchedules(
        Guid clinicId,
        Guid doctorId,
        [FromQuery] DateTime? effectiveAt = null)
    {
        var schedules = await _scheduleService.GetDoctorSchedulesAsync(clinicId, doctorId, effectiveAt);
        return Ok(schedules);
    }

    // GET: api/schedules/clinic/{clinicId}/doctor/{doctorId}/slots
    [HttpGet("clinic/{clinicId:guid}/doctor/{doctorId:guid}/slots")]
    [Authorize(Policy = "CanManageAppointments")]
    public async Task<ActionResult<List<TimeSlot>>> GetSlots(
        Guid clinicId,
        Guid doctorId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (endDate < startDate || (endDate - startDate).TotalDays > 90)
            return BadRequest("Rango de fechas inválido (máx 90 días)");

        var slots = await _scheduleService.GenerateSlotsForPeriodAsync(clinicId, doctorId, startDate, endDate);
        return Ok(slots);
    }

    // POST: api/schedules/clinic/{clinicId}/doctor/{doctorId}/slots/generate
    [HttpPost("clinic/{clinicId:guid}/doctor/{doctorId:guid}/slots/generate")]
    [Authorize(Policy = "CanManageSchedules")]
    public async Task<ActionResult<int>> GenerateSlots(
        Guid clinicId,
        Guid doctorId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        var slots = await _scheduleService.GenerateSlotsForPeriodAsync(clinicId, doctorId, startDate, endDate);
        var created = slots.Count(s => !s.IsBooked && !s.IsBlocked);
        return Ok(new { Created = created, Total = slots.Count });
    }

    // GET: api/schedules/clinic/{clinicId}/doctor/{doctorId}/availability
    [HttpGet("clinic/{clinicId:guid}/doctor/{doctorId:guid}/availability")]
    [Authorize(Policy = "CanManageAppointments")]
    public async Task<ActionResult<bool>> CheckAvailability(
        Guid clinicId,
        Guid doctorId,
        [FromQuery] DateTime startAt,
        [FromQuery] DateTime endAt,
        [FromQuery] Guid? excludeAppointmentId = null)
    {
        var available = await _scheduleService.IsSlotAvailableAsync(clinicId, doctorId, startAt, endAt, excludeAppointmentId);
        return Ok(new { Available = available });
    }

    // CRUD DoctorSchedule
    // POST: api/schedules
    [HttpPost]
    [Authorize(Policy = "CanManageSchedules")]
    public async Task<ActionResult<DoctorSchedule>> CreateSchedule([FromBody] DoctorScheduleDto dto)
    {
        var schedule = new DoctorSchedule
        {
            Id = Guid.NewGuid(),
            ClinicId = dto.ClinicId,
            DoctorId = dto.DoctorId,
            DayOfWeek = dto.DayOfWeek,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            LunchStart = dto.LunchStart,
            LunchEnd = dto.LunchEnd,
            SlotDurationMinutes = dto.SlotDurationMinutes,
            IsActive = true,
            EffectiveFrom = dto.EffectiveFrom.Date,
            EffectiveTo = dto.EffectiveTo?.Date,
            CreatedAt = DateTime.UtcNow
        };

        _context.DoctorSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSchedules), new { clinicId = schedule.ClinicId, doctorId = schedule.DoctorId }, schedule);
    }

    // PUT: api/schedules/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "CanManageSchedules")]
    public async Task<ActionResult<DoctorSchedule>> UpdateSchedule(Guid id, [FromBody] DoctorScheduleDto dto)
    {
        var schedule = await _context.DoctorSchedules.FindAsync(id);
        if (schedule == null) return NotFound();

        schedule.DayOfWeek = dto.DayOfWeek;
        schedule.StartTime = dto.StartTime;
        schedule.EndTime = dto.EndTime;
        schedule.LunchStart = dto.LunchStart;
        schedule.LunchEnd = dto.LunchEnd;
        schedule.SlotDurationMinutes = dto.SlotDurationMinutes;
        schedule.EffectiveFrom = dto.EffectiveFrom.Date;
        schedule.EffectiveTo = dto.EffectiveTo?.Date;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(schedule);
    }

    // DELETE: api/schedules/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "CanManageSchedules")]
    public async Task<ActionResult> DeleteSchedule(Guid id)
    {
        var schedule = await _context.DoctorSchedules.FindAsync(id);
        if (schedule == null) return NotFound();

        _context.DoctorSchedules.Remove(schedule);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public record DoctorScheduleDto(
    Guid ClinicId,
    Guid DoctorId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeOnly? LunchStart,
    TimeOnly? LunchEnd,
    int SlotDurationMinutes,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo
);