using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Klinara.Web.Data;

namespace Klinara.Web.Services;

public interface IClinicService
{
    Task<ClinicResult> CreateAsync(CreateClinicDto dto, Guid createdByUserId);
    Task<Clinic?> GetByIdAsync(Guid id);
    Task<Clinic?> GetBySubdomainAsync(string subdomain);
    Task<List<Clinic>> GetAllAsync(Guid? requestingUserClinicId = null, bool isPlatformAdmin = false);
    Task<ClinicResult> UpdateAsync(Guid id, UpdateClinicDto dto, Guid updatedByUserId, bool isPlatformAdmin);
    Task<ClinicResult> SetActiveAsync(Guid id, bool isActive, Guid updatedByUserId, bool isPlatformAdmin);
}

public record CreateClinicDto(
    string Name,
    string Subdomain,
    string ContactEmail,
    string? Phone,
    string? Address,
    string TimeZoneId = "America/Mexico_City"
);

public record UpdateClinicDto(
    string? Name,
    string? ContactEmail,
    string? Phone,
    string? Address,
    string? TimeZoneId,
    bool? IsActive
);

public record ClinicResult
{
    public bool Success { get; init; }
    public Clinic? Clinic { get; init; }
    public List<string> Errors { get; init; } = new();
}