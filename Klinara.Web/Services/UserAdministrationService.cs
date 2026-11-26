using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Authorization;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klinara.Web.Services;

public class UserAdministrationService : IUserAdministrationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ILogger<UserAdministrationService> _logger;

    public UserAdministrationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ILogger<UserAdministrationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<UserAdminResult> CreateAsync(CreateUserDto dto, Guid createdByUserId)
    {
        _logger.LogInformation("Creando usuario: {Email} con rol {Role} por usuario {CreatedBy}", dto.Email, dto.Role, createdByUserId);

        // Validar rol
        var validRoles = new[] { KlinaraRoles.Doctor, KlinaraRoles.Employee, KlinaraRoles.ClinicAdmin, KlinaraRoles.PlatformAdmin };
        if (!validRoles.Contains(dto.Role))
        {
            return new UserAdminResult { Success = false, Errors = new List<string> { "Rol inválido" } };
        }

        // Validar ClinicId según rol
        if (dto.Role != KlinaraRoles.PlatformAdmin)
        {
            if (!dto.ClinicId.HasValue)
            {
                return new UserAdminResult { Success = false, Errors = new List<string> { "ClinicId es requerido para roles clínicos" } };
            }

            var clinic = await _context.Clinics.FindAsync(dto.ClinicId.Value);
            if (clinic == null)
            {
                return new UserAdminResult { Success = false, Errors = new List<string> { "Clínica no encontrada" } };
            }

            // Validar que el usuario creador tiene permiso para crear en esta clínica
            var creator = await _userManager.FindByIdAsync(createdByUserId.ToString());
            if (creator == null)
            {
                return new UserAdminResult { Success = false, Errors = new List<string> { "Usuario creador no encontrado" } };
            }

            var creatorRoles = await _userManager.GetRolesAsync(creator);
            var isPlatformAdmin = creatorRoles.Contains(KlinaraRoles.PlatformAdmin);
            var isClinicAdmin = creatorRoles.Contains(KlinaraRoles.ClinicAdmin);

            if (!isPlatformAdmin)
            {
                if (!isClinicAdmin)
                {
                    return new UserAdminResult { Success = false, Errors = new List<string> { "No tiene permiso para crear usuarios" } };
                }

                // ClinicAdmin solo puede crear en su propia clínica
                if (!creator.ClinicId.HasValue || creator.ClinicId != dto.ClinicId)
                {
                    return new UserAdminResult { Success = false, Errors = new List<string> { "Solo puede crear usuarios en su propia clínica" } };
                }
            }
        }
        else
        {
            // PlatformAdmin no tiene clínica
            dto = dto with { ClinicId = null };
        }

        // Validar email único
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return new UserAdminResult { Success = false, Errors = new List<string> { "El correo ya está en uso" } };
        }

        // Crear usuario
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            EmailConfirmed = true,
            ClinicId = dto.ClinicId,
            FullName = dto.FullName,
            Phone = dto.Phone,
            CreatedAt = DateTime.UtcNow
        };

        // Generar contraseña temporal
        var tempPassword = GenerateTempPassword();
        var createResult = await _userManager.CreateAsync(user, tempPassword);
        if (!createResult.Succeeded)
        {
            return new UserAdminResult { Success = false, Errors = createResult.Errors.Select(e => e.Description).ToList() };
        }

        // Asignar rol
        var roleResult = await _userManager.AddToRoleAsync(user, dto.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return new UserAdminResult { Success = false, Errors = roleResult.Errors.Select(e => e.Description).ToList() };
        }

        // Invalidar security stamp
        await _userManager.UpdateSecurityStampAsync(user);

        _logger.LogInformation("Usuario creado: {UserId} ({Email}) con rol {Role}", user.Id, user.Email, dto.Role);

        return new UserAdminResult { Success = true, User = user };
    }

    public async Task<UserAdminResult> UpdateAsync(Guid id, UpdateUserDto dto, Guid updatedByUserId)
    {
        _logger.LogInformation("Actualizando usuario: {UserId} por usuario {UpdatedBy}", id, updatedByUserId);

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return new UserAdminResult { Success = false, Errors = new List<string> { "Usuario no encontrado" } };
        }

        // Actualizar campos permitidos
        if (!string.IsNullOrWhiteSpace(dto.FullName))
            user.FullName = dto.FullName;

        if (dto.Phone != null)
            user.Phone = dto.Phone;

        if (dto.IsActive.HasValue)
            user.IsActive = dto.IsActive.Value;

        // Role y ClinicId solo puede cambiar PlatformAdmin (validación en controller)
        // Aquí solo actualizamos si vienen en DTO y el controller ya validó permisos

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Usuario actualizado: {UserId}", id);
        return new UserAdminResult { Success = true, User = user };
    }

    public async Task<UserAdminResult> ChangeRoleAsync(Guid id, string newRole, Guid changedByUserId)
    {
        _logger.LogInformation("Cambiando rol usuario: {UserId} -> {NewRole} por usuario {ChangedBy}", id, newRole, changedByUserId);

        // Validar rol
        var validRoles = new[] { KlinaraRoles.Doctor, KlinaraRoles.Employee, KlinaraRoles.ClinicAdmin, KlinaraRoles.PlatformAdmin };
        if (!validRoles.Contains(newRole))
        {
            return new UserAdminResult { Success = false, Errors = new List<string> { "Rol inválido" } };
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return new UserAdminResult { Success = false, Errors = new List<string> { "Usuario no encontrado" } };
        }

        // No permitir auto-remover rol
        if (user.Id == changedByUserId)
        {
            return new UserAdminResult { Success = false, Errors = new List<string> { "No puede cambiar su propio rol" } };
        }

        // No quitar último PlatformAdmin
        var currentPlatformAdmins = await _userManager.GetUsersInRoleAsync(KlinaraRoles.PlatformAdmin);
        var isTargetPlatformAdmin = currentPlatformAdmins.Any(p => p.Id == user.Id);
        if (isTargetPlatformAdmin && currentPlatformAdmins.Count == 1 && newRole != KlinaraRoles.PlatformAdmin)
        {
            return new UserAdminResult { Success = false, Errors = new List<string> { "No puede quitar el último PlatformAdmin" } };
        }

        // Remover roles actuales y agregar nuevo
        var currentRoles = await _userManager.GetRolesAsync(user);
        var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
        {
            return new UserAdminResult { Success = false, Errors = removeResult.Errors.Select(e => e.Description).ToList() };
        }

        var addResult = await _userManager.AddToRoleAsync(user, newRole);
        if (!addResult.Succeeded)
        {
            return new UserAdminResult { Success = false, Errors = addResult.Errors.Select(e => e.Description).ToList() };
        }

        // Invalidar security stamp
        await _userManager.UpdateSecurityStampAsync(user);

        _logger.LogInformation("Rol cambiado: {UserId} -> {NewRole}", id, newRole);
        return new UserAdminResult { Success = true, User = user };
    }

    public async Task<UserAdminResult> ChangeClinicAsync(Guid id, Guid? newClinicId, Guid changedByUserId)
    {
        _logger.LogInformation("Cambiando clínica usuario: {UserId} -> {NewClinicId} por usuario {ChangedBy}", id, newClinicId, changedByUserId);

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return new UserAdminResult { Success = false, Errors = new List<string> { "Usuario no encontrado" } };
        }

        // Validar clínica si se proporciona
        if (newClinicId.HasValue)
        {
            var clinic = await _context.Clinics.FindAsync(newClinicId.Value);
            if (clinic == null)
            {
                return new UserAdminResult { Success = false, Errors = new List<string> { "Clínica no encontrada" } };
            }
        }

        user.ClinicId = newClinicId;
        user.UpdatedAt = DateTime.UtcNow;

        // Invalidar security stamp
        await _userManager.UpdateSecurityStampAsync(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Clínica cambiada: {UserId} -> {NewClinicId}", id, newClinicId);
        return new UserAdminResult { Success = true, User = user };
    }

    public async Task<UserAdminResult> DeactivateAsync(Guid id, Guid deactivatedByUserId)
    {
        _logger.LogInformation("Desactivando usuario: {UserId} por usuario {DeactivatedBy}", id, deactivatedByUserId);

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return new UserAdminResult { Success = false, Errors = new List<string> { "Usuario no encontrado" } };
        }

        // No auto-desactivarse
        if (user.Id == deactivatedByUserId)
        {
            return new UserAdminResult { Success = false, Errors = new List<string> { "No puede desactivarse a sí mismo" } };
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Usuario desactivado: {UserId}", id);
        return new UserAdminResult { Success = true, User = user };
    }

    public async Task<UserAdminResult> ResetPasswordAsync(Guid id, Guid requestedByUserId)
        {
            _logger.LogInformation("Reset password solicitado para usuario: {UserId} por usuario {RequestedBy}", id, requestedByUserId);

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return new UserAdminResult { Success = false, Errors = new List<string> { "Usuario no encontrado" } };
            }

            // Generar token de reset password
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Resetear password con el token (esto rota el SecurityStamp)
            // Usamos un password temporal - en producción se enviaría por email
            var tempPassword = "TempPass#123";
            var resetResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);
            if (!resetResult.Succeeded)
            {
                return new UserAdminResult { Success = false, Errors = resetResult.Errors.Select(e => e.Description).ToList() };
            }

            // Forzar rotación adicional del SecurityStamp
            await _userManager.UpdateSecurityStampAsync(user);

            _logger.LogInformation("Password reseteado para {Email}", user.Email);

            return new UserAdminResult { Success = true };
        }

    public async Task<List<ApplicationUser>> GetAllAsync(Guid? requestingUserClinicId = null, bool isPlatformAdmin = false)
    {
        var query = _context.Users.AsQueryable();

        if (!isPlatformAdmin && requestingUserClinicId.HasValue)
        {
            // ClinicAdmin solo ve su clínica (excluye PlatformAdmin que tienen ClinicId == null)
            query = query.Where(u => u.ClinicId == requestingUserClinicId.Value);
        }
        // PlatformAdmin ve todos (sin filtro)

        return await query
            .Include(u => u.Clinic)
            .OrderBy(u => u.Email)
            .ToListAsync();
    }

    public async Task<ApplicationUser?> GetByIdAsync(Guid id)
    {
        return await _userManager.FindByIdAsync(id.ToString());
    }

    private static string GenerateTempPassword()
    {
        // Generar contraseña temporal segura: 12 chars, mayúsculas, minúsculas, números, símbolos
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
        var random = new Random();
        return new string(Enumerable.Range(0, 12).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}