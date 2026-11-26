using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Authorization;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klinara.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserAdministrationService _userAdmin;

    public UsersController(IUserAdministrationService userAdmin)
    {
        _userAdmin = userAdmin;
    }

    // GET: api/users
    [HttpGet]
    [Authorize(Policy = "CanManageUsers")]
    public async Task<ActionResult<List<ApplicationUser>>> GetAll()
    {
        var user = HttpContext.User;
        var isPlatformAdmin = user.IsInRole(KlinaraRoles.PlatformAdmin);
        var clinicIdClaim = user.FindFirst("ClinicId");
        var requestingUserClinicId = clinicIdClaim != null ? Guid.Parse(clinicIdClaim.Value) : (Guid?)null;

        var users = await _userAdmin.GetAllAsync(requestingUserClinicId, isPlatformAdmin);
        return Ok(users);
    }

    // GET: api/users/{id}
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "CanManageUsers")]
    public async Task<ActionResult<ApplicationUser>> GetById(Guid id)
    {
        var user = await _userAdmin.GetByIdAsync(id);
        if (user == null)
            return NotFound();

        // ClinicAdmin solo puede ver usuarios de su clínica
        var isPlatformAdmin = HttpContext.User.IsInRole(KlinaraRoles.PlatformAdmin);
        var clinicIdClaim = HttpContext.User.FindFirst("ClinicId");
        var requestingUserClinicId = clinicIdClaim != null ? Guid.Parse(clinicIdClaim.Value) : (Guid?)null;

        if (!isPlatformAdmin && user.ClinicId != requestingUserClinicId)
            return NotFound();

        return Ok(user);
    }

    // POST: api/users
    [HttpPost]
    [Authorize(Policy = "CanManageUsers")]
    public async Task<ActionResult<ApplicationUser>> Create([FromBody] Services.CreateUserDto dto)
    {
        var userId = Guid.Parse(HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _userAdmin.CreateAsync(dto, userId);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return CreatedAtAction(nameof(GetById), new { id = result.User!.Id }, result.User);
    }

    // PUT: api/users/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "CanManageUsers")]
    public async Task<ActionResult<ApplicationUser>> Update(Guid id, [FromBody] Services.UpdateUserDto dto)
    {
        var user = HttpContext.User;
        var isPlatformAdmin = user.IsInRole(KlinaraRoles.PlatformAdmin);
        var clinicIdClaim = user.FindFirst("ClinicId");
        var requestingUserClinicId = clinicIdClaim != null ? Guid.Parse(clinicIdClaim.Value) : (Guid?)null;

        // Verificar ownership para ClinicAdmin
        if (!isPlatformAdmin)
        {
            var targetUser = await _userAdmin.GetByIdAsync(id);
            if (targetUser == null || targetUser.ClinicId != requestingUserClinicId)
                return NotFound();
        }

        var userId = Guid.Parse(HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _userAdmin.UpdateAsync(id, dto, userId);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return Ok(result.User);
    }

    // POST: api/users/{id}/change-role
    [HttpPost("{id:guid}/change-role")]
    [Authorize(Policy = "PlatformAdminOnly")]
    public async Task<ActionResult<ApplicationUser>> ChangeRole(Guid id, [FromBody] Services.ChangeRoleDto dto)
    {
        var userId = Guid.Parse(HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _userAdmin.ChangeRoleAsync(id, dto.Role, userId);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return Ok(result.User);
    }

    // POST: api/users/{id}/change-clinic
    [HttpPost("{id:guid}/change-clinic")]
    [Authorize(Policy = "PlatformAdminOnly")]
    public async Task<ActionResult<ApplicationUser>> ChangeClinic(Guid id, [FromBody] Services.ChangeClinicDto dto)
    {
        var userId = Guid.Parse(HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _userAdmin.ChangeClinicAsync(id, dto.ClinicId, userId);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return Ok(result.User);
    }

    // POST: api/users/{id}/reset-password
    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "CanManageUsers")]
    public async Task<ActionResult> ResetPassword(Guid id)
    {
        var userId = Guid.Parse(HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _userAdmin.ResetPasswordAsync(id, userId);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return Ok(new { Success = true });
    }

    // PATCH: api/users/{id}/deactivate
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = "CanManageUsers")]
    public async Task<ActionResult> Deactivate(Guid id)
    {
        var userId = Guid.Parse(HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _userAdmin.DeactivateAsync(id, userId);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return Ok(new { Success = true });
    }
}