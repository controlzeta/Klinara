using System.Security.Claims;
using Klinara.Web.Authorization;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Klinara.Web.Services;

public interface IUserAdministrationService
{
    /// <summary>
    /// Cambia el rol de un usuario y opcionalmente su clínica.
    /// </param> El usuario que ejecuta el cambio debe tener el rol PlatformAdmin o ClinicAdmin.
    /// </summary>
    Task<ChangeRoleResult> ChangeRoleAsync(ClaimsPrincipal actor, Guid userId, string newRole, Guid? newClinicId = null);

    /// <summary>
    /// Lista usuarios visibles según el rol del actor.
    /// </summary>
    Task<List<UserSummary>> ListUsersAsync(ClaimsPrincipal actor);
}

public record ChangeRoleResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public record UserSummary
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? ClinicId { get; set; }
    public string ClinicName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}