using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klinara.Web.Services;

public class ClinicService : IClinicService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ClinicService> _logger;

    public ClinicService(ApplicationDbContext context, ILogger<ClinicService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ClinicResult> CreateAsync(CreateClinicDto dto, Guid createdByUserId)
    {
        _logger.LogInformation("Creando clínica: {Subdomain} por usuario {UserId}", dto.Subdomain, createdByUserId);

        // Validar subdomain único (case-insensitive)
        var existingSubdomain = await _context.Clinics
            .AnyAsync(c => c.Subdomain.ToLower() == dto.Subdomain.ToLower());
        if (existingSubdomain)
        {
            _logger.LogWarning("Subdomain ya existe: {Subdomain}", dto.Subdomain);
            return new ClinicResult { Success = false, Errors = new List<string> { "El subdominio ya está en uso" } };
        }

        // Validar ContactEmail único (si se proporciona)
        if (!string.IsNullOrWhiteSpace(dto.ContactEmail))
        {
            var existingEmail = await _context.Clinics
                .AnyAsync(c => c.ContactEmail != null && c.ContactEmail.ToLower() == dto.ContactEmail.ToLower());
            if (existingEmail)
            {
                _logger.LogWarning("ContactEmail ya existe: {Email}", dto.ContactEmail);
                return new ClinicResult { Success = false, Errors = new List<string> { "El correo de contacto ya está en uso" } };
            }
        }

        // Validar TimeZoneId
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(dto.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("TimeZoneId inválido: {TimeZoneId}", dto.TimeZoneId);
            return new ClinicResult { Success = false, Errors = new List<string> { "Zona horaria inválida" } };
        }

        // Normalizar subdomain a lowercase
        var normalizedSubdomain = dto.Subdomain.ToLowerInvariant();

        var clinic = new Clinic
        {
            Name = dto.Name,
            Subdomain = normalizedSubdomain,
            ContactEmail = dto.ContactEmail,
            Phone = dto.Phone,
            Address = dto.Address,
            TimeZoneId = dto.TimeZoneId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Clinics.Add(clinic);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Clínica creada: {ClinicId} ({Subdomain})", clinic.Id, clinic.Subdomain);
        return new ClinicResult { Success = true, Clinic = clinic };
    }

    public async Task<Clinic?> GetByIdAsync(Guid id)
    {
        return await _context.Clinics
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Clinic?> GetBySubdomainAsync(string subdomain)
    {
        var normalized = subdomain.ToLowerInvariant();
        return await _context.Clinics
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Subdomain.ToLower() == normalized);
    }

    public async Task<List<Clinic>> GetAllAsync(Guid? requestingUserClinicId = null, bool isPlatformAdmin = false)
    {
        var query = _context.Clinics.AsNoTracking();

        if (!isPlatformAdmin && requestingUserClinicId.HasValue)
        {
            // ClinicAdmin solo ve su clínica
            query = query.Where(c => c.Id == requestingUserClinicId.Value);
        }
        // PlatformAdmin ve todas (sin filtro)

        return await query
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<ClinicResult> UpdateAsync(Guid id, UpdateClinicDto dto, Guid updatedByUserId, bool isPlatformAdmin)
    {
        _logger.LogInformation("Actualizando clínica: {ClinicId} por usuario {UserId}", id, updatedByUserId);

        var clinic = await _context.Clinics.FindAsync(id);
        if (clinic == null)
        {
            return new ClinicResult { Success = false, Errors = new List<string> { "Clínica no encontrada" } };
        }

        _logger.LogInformation("Clinic found: {ClinicId}, CurrentEmail: {CurrentEmail}, DtoEmail: {DtoEmail}", clinic.Id, clinic.ContactEmail, dto.ContactEmail);

        // Verificar permisos: ClinicAdmin solo puede actualizar su clínica
        if (!isPlatformAdmin)
        {
            // En un caso real, aquí validaríamos que el usuario pertenece a esta clínica
            // El controller ya valida la policy CanManageUsers y el ownership
        }

        // Validar ContactEmail único si se está cambiando
        if (!string.IsNullOrWhiteSpace(dto.ContactEmail) && dto.ContactEmail != clinic.ContactEmail)
        {
            _logger.LogInformation("Email change detected, checking for duplicates: {DtoEmail} vs {CurrentEmail}", dto.ContactEmail, clinic.ContactEmail);
            var existingEmail = await _context.Clinics
                .AnyAsync(c => c.Id != id && c.ContactEmail != null && c.ContactEmail.ToLower() == dto.ContactEmail!.ToLower());
            _logger.LogInformation("Duplicate email check result: {ExistingEmail}", existingEmail);
            if (existingEmail)
            {
                _logger.LogWarning("Duplicate email found: {Email}", dto.ContactEmail);
                return new ClinicResult { Success = false, Errors = new List<string> { "El correo de contacto ya está en uso" } };
            }
        }

        // Validar TimeZoneId si se proporciona
        if (!string.IsNullOrWhiteSpace(dto.TimeZoneId))
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(dto.TimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                return new ClinicResult { Success = false, Errors = new List<string> { "Zona horaria inválida" } };
            }
        }

        // Actualizar campos (Subdomain NO se puede cambiar)
        if (!string.IsNullOrWhiteSpace(dto.Name))
            clinic.Name = dto.Name;
        
        if (dto.ContactEmail != null)
            clinic.ContactEmail = dto.ContactEmail;
        
        if (dto.Phone != null)
            clinic.Phone = dto.Phone;
        
        if (dto.Address != null)
            clinic.Address = dto.Address;
        
        if (!string.IsNullOrWhiteSpace(dto.TimeZoneId))
            clinic.TimeZoneId = dto.TimeZoneId;
        
        if (dto.IsActive.HasValue)
            clinic.IsActive = dto.IsActive.Value;

        clinic.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Clínica actualizada: {ClinicId}", id);
        return new ClinicResult { Success = true, Clinic = clinic };
    }

    public async Task<ClinicResult> SetActiveAsync(Guid id, bool isActive, Guid updatedByUserId, bool isPlatformAdmin)
    {
        _logger.LogInformation("Cambiando estado clínica: {ClinicId} -> IsActive={IsActive} por usuario {UserId}", id, isActive, updatedByUserId);

        if (!isPlatformAdmin)
        {
            return new ClinicResult { Success = false, Errors = new List<string> { "Solo administradores de plataforma pueden activar/desactivar clínicas" } };
        }

        var clinic = await _context.Clinics.FindAsync(id);
        if (clinic == null)
        {
            return new ClinicResult { Success = false, Errors = new List<string> { "Clínica no encontrada" } };
        }

        clinic.IsActive = isActive;
        clinic.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Clínica {ClinicId} {Action}", id, isActive ? "activada" : "desactivada");
        return new ClinicResult { Success = true, Clinic = clinic };
    }
}