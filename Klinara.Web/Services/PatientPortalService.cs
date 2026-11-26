using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Klinara.Web.Services;

public class PatientPortalService : IPatientPortalService
{
    private readonly ApplicationDbContext _context;
    private readonly IScheduleService _scheduleService;
    private readonly IAppointmentService _appointmentService;
    private readonly IPatientTokenService _patientTokenService;
    private readonly IOtpService _otpService;
    private readonly OtpEmailSender _otpEmailSender;
    private readonly IEmailSender<ApplicationUser> _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PatientPortalService> _logger;

    public PatientPortalService(
        ApplicationDbContext context,
        IScheduleService scheduleService,
        IAppointmentService appointmentService,
        IPatientTokenService patientTokenService,
        IOtpService otpService,
        OtpEmailSender otpEmailSender,
        IEmailSender<ApplicationUser> emailSender,
        IConfiguration configuration,
        ILogger<PatientPortalService> logger)
    {
        _context = context;
        _scheduleService = scheduleService;
        _appointmentService = appointmentService;
        _patientTokenService = patientTokenService;
        _otpService = otpService;
        _otpEmailSender = otpEmailSender;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PortalVerificationResult> SendVerificationEmailAsync(string email, string clinicSubdomain)
    {
        try
        {
            // Buscar clínica por subdominio
            var clinic = await _context.Clinics
                .FirstOrDefaultAsync(c => c.Subdomain == clinicSubdomain && c.IsActive);

            if (clinic == null)
            {
                _logger.LogWarning("Clínica no encontrada para subdominio: {Subdomain}", clinicSubdomain);
                return new PortalVerificationResult(false, ErrorCodes.ClinicNotFound, email);
            }

            // Buscar o crear paciente por email en esta clínica
            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.ClinicId == clinic.Id && p.Email == email);

            if (patient == null)
            {
                // Crear paciente temporal (sin usuario Identity por ahora)
                patient = new Patient
                {
                    Id = Guid.NewGuid(),
                    ClinicId = clinic.Id,
                    FullName = "Paciente Nuevo", // Se actualizará en reserva
                    Email = email,
                    CreatedByUserId = Guid.Empty, // System
                    CreatedAt = DateTime.UtcNow
                };
                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Paciente temporal creado: {PatientId} para email {Email} en clínica {ClinicId}", patient.Id, email, clinic.Id);
            }

            // Verificar si el paciente está bloqueado
            if (patient.IsBlocked && patient.BlockedUntil.HasValue && patient.BlockedUntil.Value > DateTime.UtcNow)
            {
                return new PortalVerificationResult(false, ErrorCodes.PatientBlocked, email);
            }

            // Buscar o crear ApplicationUser para enviar OTP (usuario "shadow" para portal)
            var portalUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.ClinicId == clinic.Id);

            if (portalUser == null)
            {
                portalUser = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = false, // Se confirmará con OTP
                    ClinicId = clinic.Id,
                    OtpFailedAttempts = 0
                };
                _context.Users.Add(portalUser);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Usuario portal creado: {UserId} para email {Email}", portalUser.Id, email);
            }

            // Generar y enviar un único OTP con el propósito de verificación de email (scoped a clínica)
                        var otpPurpose = OtpPurposes.EmailVerificationForClinic(clinic.Id);
                        var otpCode = await _otpService.GenerateOtpAsync(portalUser, otpPurpose);

            // Enviar email real (SmtpEmailSender en prod, LoggingEmailSender en dev)
            await SendOtpEmailAsync(email, otpCode, clinic.Name);

            _logger.LogInformation("OTP de verificación enviado a {Email} para clínica {ClinicId}", email, clinic.Id);

            return new PortalVerificationResult(true, null, email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando email de verificación a {Email} para subdominio {Subdomain}", email, clinicSubdomain);
            return new PortalVerificationResult(false, ErrorCodes.OtpSendError, email);
        }
    }

    public async Task<PortalTokenResult> ValidateOtpAndCreatePortalTokenAsync(string email, string code, string clinicSubdomain)
    {
        try
        {
            var clinic = await _context.Clinics
                .FirstOrDefaultAsync(c => c.Subdomain == clinicSubdomain && c.IsActive);

            if (clinic == null)
            {
                return new PortalTokenResult(false, Error: ErrorCodes.ClinicNotFound);
            }

            var portalUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.ClinicId == clinic.Id);

            if (portalUser == null)
            {
                return new PortalTokenResult(false, Error: ErrorCodes.UserNotFound);
            }

            // Validar OTP (con propósito scoped a clínica)
                        var otpPurpose = OtpPurposes.EmailVerificationForClinic(clinic.Id);
                        var isValid = await _otpService.ValidateOtpAsync(portalUser, code, otpPurpose);

                        if (!isValid)
                        {
                            _logger.LogWarning("OTP inválido para {Email} en clínica {ClinicId}", email, clinic.Id);
                            return new PortalTokenResult(false, Error: ErrorCodes.OtpInvalid);
                        }

                        // Invalidar OTP usado
                        await _otpService.InvalidateOtpAsync(portalUser, otpPurpose);

            // Buscar paciente
            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.ClinicId == clinic.Id && p.Email == email);

            if (patient == null)
            {
                return new PortalTokenResult(false, Error: ErrorCodes.PatientNotFound);
            }

            // Verificar bloqueo
            if (patient.IsBlocked && patient.BlockedUntil.HasValue && patient.BlockedUntil.Value > DateTime.UtcNow)
            {
                return new PortalTokenResult(false, Error: ErrorCodes.PatientBlocked);
            }

            // Crear token PortalAccess (30 min, single-use)
            var portalToken = await _patientTokenService.CreatePortalAccessTokenAsync(patient.Id, clinic.Id);

            // Marcar email como confirmado en el usuario portal
            portalUser.EmailConfirmed = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("PortalAccess token creado para PatientId: {PatientId}, Token: {TokenPrefix}...", patient.Id, portalToken.Token[..8]);

            return new PortalTokenResult(
                true,
                portalToken.Token,
                portalToken.ExpiresAt,
                null,
                patient.Id
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando OTP y creando token portal para {Email}", email);
            return new PortalTokenResult(false, Error: "Error interno al validar el código");
        }
    }

    public async Task<PortalBookingResult> BookAppointmentAsync(string portalToken, BookAppointmentDto dto)
    {
        try
        {
            // Validar y consumir token PortalAccess
            var patientToken = await _patientTokenService.ValidateAndConsumeTokenAsync(portalToken, PatientTokenType.PortalAccess);

            if (patientToken == null)
            {
                return new PortalBookingResult(false, Error: ErrorCodes.PortalAccessTokenInvalid);
            }

            var patient = patientToken.Patient;
            if (patient == null)
            {
                patient = await _context.Patients.FindAsync(patientToken.PatientId);
            }

            if (patient == null)
            {
                return new PortalBookingResult(false, Error: ErrorCodes.PatientNotFound);
            }

            // Verificar bloqueo (doble check)
            if (patient.IsBlocked && patient.BlockedUntil.HasValue && patient.BlockedUntil.Value > DateTime.UtcNow)
            {
                return new PortalBookingResult(false, Error: ErrorCodes.PatientBlocked);
            }

            // Verificar que el doctor pertenece a la clínica
                        var doctor = await _context.Users
                            .FirstOrDefaultAsync(u => u.Id == dto.DoctorId && u.ClinicId == patient.ClinicId);

                        if (doctor == null)
                        {
                            return new PortalBookingResult(false, Error: ErrorCodes.DoctorNotFoundInClinic);
                        }

                        // Buscar el usuario portal (ApplicationUser) para usar su ID en CreatedByUserId
                        var portalUser = await _context.Users
                            .FirstOrDefaultAsync(u => u.Email == patient.Email && u.ClinicId == patient.ClinicId);

                        if (portalUser == null)
                        {
                            _logger.LogError("Usuario portal no encontrado para PatientId: {PatientId}, Email: {Email}, ClinicId: {ClinicId}", patient.Id, patient.Email, patient.ClinicId);
                            return new PortalBookingResult(false, Error: "Error de configuración del paciente");
                        }

                        // Crear cita usando AppointmentService (validaciones incluidas: horizonte 45d, traslapes, etc.)
                        var appointment = new Appointment
                        {
                            ClinicId = patient.ClinicId,
                            DoctorId = dto.DoctorId,
                            PatientId = patient.Id,
                            StartAt = dto.StartAt,
                            EndAt = dto.EndAt,
                            Type = dto.Type,
                            Notes = dto.Notes,
                            CreatedByUserId = portalUser.Id, // Usar ID del usuario portal (ApplicationUser)
                            Status = AppointmentStatus.Scheduled
                        };

            // Usar el método extendido para portal (isPatientBooking = true)
            var result = await _appointmentService.CreateAppointmentAsync(appointment, isPatientBooking: true);

            if (!result.Success)
            {
                return new PortalBookingResult(false, Error: string.Join("; ", result.Errors));
            }

            var createdAppointment = result.Appointment!;

            // Crear token de confirmación de cita (24h, single-use)
            var confirmationToken = await _patientTokenService.CreateAppointmentConfirmationTokenAsync(
                patient.Id, createdAppointment.Id, patient.ClinicId);

            // Crear token de gestión de cita (45d, para consultar/cancelar cita específica)
            var managementToken = await _patientTokenService.CreateAppointmentManagementTokenAsync(
                patient.Id, createdAppointment.Id, patient.ClinicId);

            // Enviar email de confirmación
            await SendAppointmentConfirmationEmailAsync(patient.Email!, confirmationToken.Token, createdAppointment, doctor.UserName!);

            _logger.LogInformation("Cita creada desde portal: {AppointmentId} para PatientId: {PatientId}", createdAppointment.Id, patient.Id);

            return new PortalBookingResult(
                true,
                createdAppointment.Id,
                confirmationToken.Token,
                confirmationToken.ExpiresAt,
                null,
                managementToken.Token,
                managementToken.ExpiresAt
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reservando cita desde portal con token {TokenPrefix}...", portalToken[..Math.Min(8, portalToken.Length)]);
            return new PortalBookingResult(false, Error: "Error interno al reservar la cita");
        }
    }

    public async Task<PortalConfirmationResult> ConfirmAppointmentAsync(string confirmationToken)
    {
        try
        {
            // Validar y consumir token AppointmentConfirmation
            var patientToken = await _patientTokenService.ValidateAndConsumeTokenAsync(confirmationToken, PatientTokenType.AppointmentConfirmation);

            if (patientToken == null)
            {
                return new PortalConfirmationResult(false, Error: ErrorCodes.ConfirmationTokenInvalid);
            }

            if (!patientToken.AppointmentId.HasValue)
            {
                return new PortalConfirmationResult(false, Error: "Token no asociado a una cita");
            }

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == patientToken.AppointmentId.Value);

            if (appointment == null)
            {
                return new PortalConfirmationResult(false, Error: ErrorCodes.AppointmentNotFound);
            }

            if (appointment.Status != AppointmentStatus.Scheduled)
            {
                return new PortalConfirmationResult(false, Error: ErrorCodes.AppointmentNotScheduled);
            }

            // Confirmar cita
            appointment.Status = AppointmentStatus.Confirmed;
            appointment.UpdatedAt = DateTime.UtcNow;

            _context.AppointmentHistory.Add(new AppointmentHistory
            {
                AppointmentId = appointment.Id,
                FromStatus = AppointmentStatus.Scheduled,
                ToStatus = AppointmentStatus.Confirmed,
                Reason = "Confirmada por paciente via portal",
                ChangedByUserId = appointment.PatientId,
                At = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cita confirmada por paciente: {AppointmentId}", appointment.Id);

            return new PortalConfirmationResult(true, appointment.Id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirmando cita con token {TokenPrefix}...", confirmationToken[..Math.Min(8, confirmationToken.Length)]);
            return new PortalConfirmationResult(false, Error: "Error interno al confirmar la cita");
        }
    }

    public async Task<PortalAppointmentView?> GetAppointmentViewAsync(string portalToken)
    {
        try
        {
            // 1. Intentar con token de gestión de cita
            var patientToken = await _patientTokenService.ValidateTokenAsync(portalToken, PatientTokenType.AppointmentManagement);

            // 2. Intentar con token de confirmación (incluso si ya fue consumido)
            if (patientToken == null)
            {
                patientToken = await _patientTokenService.ValidateTokenAsync(portalToken, PatientTokenType.AppointmentConfirmation, allowUsed: true);
            }

            // 3. Intentar con token de portal
            if (patientToken == null)
            {
                patientToken = await _patientTokenService.ValidateTokenAsync(portalToken, PatientTokenType.PortalAccess, allowUsed: true);
            }

            if (patientToken == null)
            {
                return null;
            }

            Guid appointmentId;
            if (patientToken.AppointmentId.HasValue)
            {
                appointmentId = patientToken.AppointmentId.Value;
            }
            else
            {
                // Buscar la cita más reciente del paciente dentro de su clínica
                var latestAppointment = await _context.Appointments
                    .Where(a => a.PatientId == patientToken.PatientId && a.ClinicId == patientToken.ClinicId)
                    .OrderByDescending(a => a.StartAt)
                    .FirstOrDefaultAsync();

                if (latestAppointment == null) return null;
                appointmentId = latestAppointment.Id;
            }

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .Include(a => a.Clinic)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.ClinicId == patientToken.ClinicId && a.PatientId == patientToken.PatientId);

            if (appointment == null) return null;

            return new PortalAppointmentView(
                appointment.Id,
                appointment.ClinicId,
                appointment.Clinic?.Name ?? "Clínica",
                appointment.Doctor?.UserName ?? "Doctor",
                appointment.Patient?.FullName ?? "Paciente",
                appointment.StartAt,
                appointment.EndAt,
                appointment.Type,
                appointment.Status,
                appointment.Notes,
                appointment.CancellationReason
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo vista de cita para token {TokenPrefix}...", portalToken[..Math.Min(8, portalToken.Length)]);
            return null;
        }
    }

    public async Task<PortalCancellationResult> CancelAppointmentAsync(string portalToken, string reason)
    {
        try
        {
            // 1. Intentar validar y consumir token de gestión de cita
            var patientToken = await _patientTokenService.ValidateAndConsumeTokenAsync(portalToken, PatientTokenType.AppointmentManagement);

            // 2. Si no es gestión, probar token de confirmación
            if (patientToken == null)
            {
                patientToken = await _patientTokenService.ValidateAndConsumeTokenAsync(portalToken, PatientTokenType.AppointmentConfirmation);
            }

            // 3. Si no, probar token de acceso de portal
            if (patientToken == null)
            {
                patientToken = await _patientTokenService.ValidateAndConsumeTokenAsync(portalToken, PatientTokenType.PortalAccess);
            }

            if (patientToken == null)
            {
                return new PortalCancellationResult(false, Error: ErrorCodes.PortalAccessTokenInvalid);
            }

            var patient = patientToken.Patient ?? await _context.Patients.FindAsync(patientToken.PatientId);
            if (patient == null)
            {
                return new PortalCancellationResult(false, Error: ErrorCodes.PatientNotFound);
            }

            // Buscar la cita específica si el token la tiene
            Appointment? appointment = null;
            if (patientToken.AppointmentId.HasValue)
            {
                appointment = await _context.Appointments
                    .Include(a => a.Patient)
                    .FirstOrDefaultAsync(a => a.Id == patientToken.AppointmentId.Value
                        && a.ClinicId == patientToken.ClinicId
                        && a.PatientId == patient.Id
                        && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed));
            }

            if (appointment == null)
            {
                // Buscar la cita próxima activa del paciente
                appointment = await _context.Appointments
                    .Include(a => a.Patient)
                    .Where(a => a.PatientId == patient.Id
                        && a.ClinicId == patientToken.ClinicId
                        && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed)
                        && a.StartAt > DateTime.UtcNow)
                    .OrderBy(a => a.StartAt)
                    .FirstOrDefaultAsync();
            }

            if (appointment == null)
            {
                return new PortalCancellationResult(false, Error: ErrorCodes.NoScheduledAppointments);
            }

            // Cancelar usando AppointmentService (isPatientCancellation = true)
            var result = await _appointmentService.CancelAppointmentAsync(
                appointment.Id, patient.Id, reason, isPatientCancellation: true);

            if (!result.Success)
            {
                return new PortalCancellationResult(false, Error: string.Join("; ", result.Errors));
            }

            var isLate = result.Appointment!.IsLateCancellation;
            var lateCount = patient.LateCancellationCount;
            var isBlocked = patient.IsBlocked;

            _logger.LogInformation("Cita cancelada por paciente desde portal: {AppointmentId}, Late: {IsLate}, Count: {LateCount}",
                appointment.Id, isLate, lateCount);

            return new PortalCancellationResult(
                true,
                isLate,
                lateCount,
                isBlocked,
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelando cita desde portal con token {TokenPrefix}...", portalToken[..Math.Min(8, portalToken.Length)]);
            return new PortalCancellationResult(false, Error: ErrorCodes.OtpSendError);
        }
    }

    public async Task<List<DoctorAvailabilityDto>> GetAvailableDoctorsAsync(string clinicSubdomain)
    {
        var clinic = await _context.Clinics
            .FirstOrDefaultAsync(c => c.Subdomain == clinicSubdomain && c.IsActive);

        if (clinic == null)
            return new List<DoctorAvailabilityDto>();

        var doctors = await _context.Users
            .Where(u => u.ClinicId == clinic.Id)
            .ToListAsync();

        var result = new List<DoctorAvailabilityDto>();

        foreach (var doctor in doctors)
        {
            var schedules = await _context.DoctorSchedules
                .Where(ds => ds.ClinicId == clinic.Id && ds.DoctorId == doctor.Id && ds.IsActive)
                .OrderBy(ds => ds.DayOfWeek)
                .ThenBy(ds => ds.StartTime)
                .ToListAsync();

            var daySchedules = schedules.Select(s => new DayScheduleDto(
                s.DayOfWeek,
                s.StartTime,
                s.EndTime,
                s.LunchStart,
                s.LunchEnd,
                s.SlotDurationMinutes
            )).ToList();

            result.Add(new DoctorAvailabilityDto(
                doctor.Id,
                doctor.UserName ?? "Doctor",
                null, // Specialty - no existe en modelo aún
                daySchedules
            ));
        }

        return result;
    }

    public async Task<List<TimeSlot>> GetAvailableSlotsAsync(string clinicSubdomain, Guid doctorId, DateTime startDate, DateTime endDate)
    {
        var clinic = await _context.Clinics
            .FirstOrDefaultAsync(c => c.Subdomain == clinicSubdomain && c.IsActive);

        if (clinic == null)
            return new List<TimeSlot>();

        // Generar slots usando ScheduleService
        var slots = await _scheduleService.GenerateSlotsForPeriodAsync(clinic.Id, doctorId, startDate, endDate);

        // Filtrar solo disponibles (no reservados, no bloqueados)
        return slots.Where(s => !s.IsBooked && !s.IsBlocked).ToList();
    }

    // Métodos privados auxiliares

    private async Task SendOtpEmailAsync(string email, string otpCode, string clinicName)
        {
            var user = new ApplicationUser { Email = email, UserName = email };
            // Envía el código OTP de verificación usando la plantilla Klinara
            await _emailSender.SendPasswordResetCodeAsync(user, email, otpCode);
        }

        private async Task SendAppointmentConfirmationEmailAsync(string email, string confirmationToken, Appointment appointment, string doctorName)
        {
            var baseUrl = _configuration["PatientPortal:BaseUrl"] ?? "https://localhost:5000";
            var subdomain = appointment.Clinic?.Subdomain ?? "";
            var confirmUrl = $"{baseUrl}/portal/{subdomain}/confirm/{confirmationToken}";
            var clinicName = appointment.Clinic?.Name ?? "Klinara";
            var patientName = appointment.Patient?.FullName ?? "Paciente";
            var duration = appointment.EndAt - appointment.StartAt;

            var subject = $"Confirma tu cita - {clinicName}";
                        var body = KlinaraEmailTemplates.AppointmentConfirmation(
                            patientName,
                            doctorName,
                            appointment.StartAt,
                            duration,
                            appointment.Type.ToString(),
                            appointment.Notes,
                            confirmUrl,
                            clinicName,
                            subdomain
                        );

            var user = new ApplicationUser { Email = email, UserName = email };
            await _emailSender.SendConfirmationLinkAsync(user, email, confirmUrl);
        }
    }