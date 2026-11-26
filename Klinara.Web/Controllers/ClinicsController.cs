using System;
using System.Collections.Generic;
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
public class ClinicsController : ControllerBase
{
    private readonly IClinicService _clinicService;

    public ClinicsController(IClinicService clinicService)
    {
        _clinicService = clinicService;
    }

    // GET: api/clinics
    [HttpGet]
    [Authorize(Policy = "CanManageUsers")]
    public async Task<ActionResult<List<Clinic>>> GetAll()
    {
        var user = HttpContext.User;
        var isPlatformAdmin = user.IsInRole(KlinaraRoles.PlatformAdmin);
        var clinicIdClaim = user.FindFirst("ClinicId");
        var requestingUserClinicId = clinicIdClaim != null ? Guid.Parse(clinicIdClaim.Value) : (Guid?)null;

        var clinics = await _clinicService.GetAllAsync(requestingUserClinicId, isPlatformAdmin);
        return Ok(clinics);
    }

    // GET: api/clinics/{id}
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "CanManageUsers")]
    public async Task<ActionResult<Clinic>> GetById(Guid id)
    {
        var user = HttpContext.User;
        var isPlatformAdmin = user.IsInRole(KlinaraRoles.PlatformAdmin);
        var clinicIdClaim = user.FindFirst("ClinicId");
        var requestingUserClinicId = clinicIdClaim != null ? Guid.Parse(clinicIdClaim.Value) : (Guid?)null;

        var clinic = await _clinicService.GetByIdAsync(id);
        if (clinic == null)
            return NotFound();

        // ClinicAdmin solo puede ver su clínica
        if (!isPlatformAdmin && clinic.Id != requestingUserClinicId)
            return NotFound();

        return Ok(clinic);
    }

    // GET: api/clinics/subdomain/{subdomain} (público para portal)
    [HttpGet("subdomain/{subdomain}")]
    [AllowAnonymous]
    public async Task<ActionResult<Clinic>> GetBySubdomain(string subdomain)
    {
        var clinic = await _clinicService.GetBySubdomainAsync(subdomain);
        if (clinic == null)
            return NotFound();

        return Ok(clinic);
    }

    // POST: api/clinics
    [HttpPost]
    [Authorize(Policy = "PlatformAdminOnly")]
    public async Task<ActionResult<Clinic>> Create([FromBody] CreateClinicDto dto)
    {
        var userId = Guid.Parse(HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _clinicService.CreateAsync(dto, userId);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return CreatedAtAction(nameof(GetById), new { id = result.Clinic!.Id }, result.Clinic);
    }

    // PUT: api/clinics/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "CanManageUsers")]
    public async Task<ActionResult<Clinic>> Update(Guid id, [FromBody] UpdateClinicDto dto)
    {
        var user = HttpContext.User;
        var isPlatformAdmin = user.IsInRole(KlinaraRoles.PlatformAdmin);
        var clinicIdClaim = user.FindFirst("ClinicId");
        var requestingUserClinicId = clinicIdClaim != null ? Guid.Parse(clinicIdClaim.Value) : (Guid?)null;

        // Verificar ownership para ClinicAdmin
        if (!isPlatformAdmin)
        {
            var clinic = await _clinicService.GetByIdAsync(id);
            if (clinic == null || clinic.Id != requestingUserClinicId)
                return NotFound();
        }

        var userId = Guid.Parse(HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _clinicService.UpdateAsync(id, dto, userId, isPlatformAdmin);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return Ok(result.Clinic);
    }

    // PATCH: api/clinics/{id}/activate
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = "PlatformAdminOnly")]
    public async Task<ActionResult> SetActive(Guid id, [FromBody] bool isActive)
    {
        var userId = Guid.Parse(HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _clinicService.SetActiveAsync(id, isActive, userId, true);

        if (!result.Success)
            return BadRequest(new { Errors = result.Errors });

        return Ok(new { Success = true, IsActive = isActive });
    }
}