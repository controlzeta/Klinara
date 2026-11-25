using System.Security.Claims;
using Klinara.Web.Authorization;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Klinara.Web.Services;

public class UserAdministrationService : IUserAdministrationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public UserAdministrationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<ChangeRoleResult> ChangeRoleAsync(ClaimsPrincipal actor, Guid userId, string newRole, Guid? newClinicId = null)
    {
        // 1. Validar que el actor tiene permiso
        var actorUser = await _userManager.GetUserAsync(actor);
        if (actorUser == null) return new ChangeRoleResult { Success = false, Error = "Usuario actor no encontrado" };

        // Verificar rol del actor: solo PlatformAdmin y ClinicAdmin pueden cambiar roles
        var actorRoles = await _userManager.GetRolesAsync(actorUser);
        bool isPlatformAdmin = actorRoles.Contains(KlinaraRoles.PlatformAdmin);
        bool isClinicAdmin = actorRoles.Contains(KlinaraRoles.ClinicAdmin);

        if (!isPlatformAdmin && !isClinicAdmin)
            return new ChangeRoleResult { Success = false, Error = "No tiene permiso para cambiar roles" };

        // 2. No permitir que un usuario quite su propio rol (protege al último admin)
        if (actorUser.Id == userId)
            return new ChangeRoleResult { Success = false, Error = "No puede cambiar su propio rol" };

        // 3. Si el actor es ClinicAdmin, restringir a su propia clínica
        if (isClinicAdmin && actorUser.ClinicId.HasValue && actorUser.ClinicId != newClinicId)
            return new ChangeRoleResult { Success = false, Error = "Sólo puede cambiar roles en su propia clínica" };

        // 4. Buscar el usuario objetivo
        var targetUser = await _userManager.FindByIdAsync(userId.ToString());
        if (targetUser == null)
            return new ChangeRoleResult { Success = false, Error = "Usuario objetivo no encontrado" };

        // 5. No permitir remover el último PlatformAdmin (solo si intentamos cambiar el rol del propio admin)
        var currentPlatformAdmins = await _userManager.GetUsersInRoleAsync(KlinaraRoles.PlatformAdmin);
        if (currentPlatformAdmins.Count == 1 && actorUser.ClinicId == null)
        {
            var isTargetCurrentPlatformAdmin = currentPlatformAdmins.Any(p => p.Id == targetUser.Id);
            if (isTargetCurrentPlatformAdmin && newRole != KlinaraRoles.PlatformAdmin)
                return new ChangeRoleResult { Success = false, Error = "No puede quitar el último PlatformAdmin" };
        }

        // 6. Remover roles antiguos y agregar el nuevo
        var currentRoles = await _userManager.GetRolesAsync(targetUser);
        var removeResult = await _userManager.RemoveFromRolesAsync(targetUser, currentRoles);
        if (!removeResult.Succeeded)
            return new ChangeRoleResult { Success = false, Error = string.Join("; ", removeResult.Errors.Select(e => e.Description)) };

        var addResult = await _userManager.AddToRoleAsync(targetUser, newRole);
        if (!addResult.Succeeded)
            return new ChangeRoleResult { Success = false, Error = string.Join("; ", addResult.Errors.Select(e => e.Description)) };

        // 7. Actualizar ClinicId si se proporcionó y el actor tiene permiso
        if (newClinicId.HasValue)
        {
            // ClinicAdmin no puede cambiar ClinicId a otro valor
            if (isClinicAdmin) return new ChangeRoleResult { Success = false, Error = "ClinicAdmin no puede cambiar la clínica del usuario" };

            // PlatformAdmin puede cambiar a cualquier clínica
            targetUser.ClinicId = newClinicId.Value;
            await _context.SaveChangesAsync();
        }

        // 8. Invalidar security stamp para reflejar el cambio inmediato
        await _userManager.UpdateSecurityStampAsync(targetUser);

        return new ChangeRoleResult { Success = true };
    }

    public async Task<List<UserSummary>> ListUsersAsync(ClaimsPrincipal actor)
    {
        var actorUser = await _userManager.GetUserAsync(actor);
        if (actorUser == null) return new List<UserSummary>();

        var actorRoles = await _userManager.GetRolesAsync(actorUser);
        bool isPlatformAdmin = actorRoles.Contains(KlinaraRoles.PlatformAdmin);
        bool isClinicAdmin = actorRoles.Contains(KlinaraRoles.ClinicAdmin);

        IQueryable<ApplicationUser> query = _context.Users.Include(u => u.Clinic);

        if (!isPlatformAdmin && !isClinicAdmin)
        {
            // Usuarios sin permiso ven solo a sí mismos
            query = query.Where(u => u.Id == actorUser.Id);
        }
        else if (isClinicAdmin && !isPlatformAdmin)
        {
            // ClinicAdmin ve solo usuarios de su clínica
            query = query.Where(u => u.ClinicId == actorUser.ClinicId);
        }
        // PlatformAdmin ve todos

        var users = await query.ToListAsync();

        return users.Select(u => new UserSummary
        {
            Id = u.Id,
            UserName = u.UserName ?? string.Empty,
            Email = u.Email ?? string.Empty,
            ClinicId = u.ClinicId,
            ClinicName = u.Clinic?.Name ?? string.Empty,
            Roles = (_userManager.GetRolesAsync(u).Result).ToList()
        }).ToList();
    }
}