using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Klinara.Web.Authorization;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Klinara.Web.Services;

public interface IUserAdministrationService
{
    /// <summary>
    /// Creates a new user with role and clinic assignment.
    /// </summary>
    Task<UserAdminResult> CreateAsync(CreateUserDto dto, Guid createdByUserId);

    /// <summary>
    /// Updates user profile (name, phone, isActive). PlatformAdmin can also change Role and ClinicId.
    /// </summary>
    Task<UserAdminResult> UpdateAsync(Guid id, UpdateUserDto dto, Guid updatedByUserId);

    /// <summary>
    /// Changes user role (PlatformAdmin only). Invalidates SecurityStamp.
    /// </summary>
    Task<UserAdminResult> ChangeRoleAsync(Guid id, string newRole, Guid changedByUserId);

    /// <summary>
    /// Changes user clinic assignment (PlatformAdmin only). Invalidates SecurityStamp.
    /// </summary>
    Task<UserAdminResult> ChangeClinicAsync(Guid id, Guid? newClinicId, Guid changedByUserId);

    /// <summary>
    /// Soft deactivates user (sets IsActive=false).
    /// </summary>
    Task<UserAdminResult> DeactivateAsync(Guid id, Guid deactivatedByUserId);

    /// <summary>
    /// Generates OTP for password reset and sends via email.
    /// </summary>
    Task<UserAdminResult> ResetPasswordAsync(Guid id, Guid requestedByUserId);

    /// <summary>
    /// Lists users visible according to actor's role.
    /// PlatformAdmin: all users. ClinicAdmin: only own clinic (excludes PlatformAdmin).
    /// </summary>
    Task<List<ApplicationUser>> GetAllAsync(Guid? requestingUserClinicId = null, bool isPlatformAdmin = false);

    /// <summary>
    /// Gets user by ID.
    /// </summary>
    Task<ApplicationUser?> GetByIdAsync(Guid id);
}

/// <summary>
/// DTO for creating a new user.
/// </summary>
public record CreateUserDto(
    string Email,
    string FullName,
    string Role,           // "Doctor" | "Empleado" | "AdminClinica" (PlatformAdmin can create any)
    Guid? ClinicId,        // Required for clinical roles, null for PlatformAdmin
    string? Phone
);

/// <summary>
/// DTO for updating user profile.
/// </summary>
public record UpdateUserDto(
    string? FullName,
    string? Phone,
    bool? IsActive,
    string? Role,          // Only PlatformAdmin can change
    Guid? ClinicId         // Only PlatformAdmin can change
);

/// <summary>
/// Result of user administration operations.
/// </summary>
public record UserAdminResult
{
    public bool Success { get; init; }
    public ApplicationUser? User { get; init; }
    public List<string> Errors { get; init; } = new();
}

/// <summary>
/// DTO for changing user role.
/// </summary>
public record ChangeRoleDto(string Role);

/// <summary>
/// DTO for changing user clinic assignment.
/// </summary>
public record ChangeClinicDto(Guid? ClinicId);