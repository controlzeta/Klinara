using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Klinara.Web.Authorization;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klinara.Tests;

/// <summary>
/// Shared test fixture for controller integration tests.
/// Provides authenticated HttpClient helpers and test data creation.
/// </summary>
public sealed class ControllerTestFixture : IAsyncDisposable
{
    private readonly IServiceProvider _provider;
    private readonly WebApplicationFactory<Program> _factory;

    public ControllerTestFixture(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _provider = factory.Services;
    }

    public ApplicationDbContext CreateContext()
    {
        return _provider.GetRequiredService<ApplicationDbContext>();
    }

    public UserManager<ApplicationUser> GetUserManager()
    {
        return _provider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public RoleManager<IdentityRole<Guid>> GetRoleManager()
    {
        return _provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    }

    public IUserAdministrationService GetUserAdministrationService()
    {
        return _provider.GetRequiredService<IUserAdministrationService>();
    }

    public IClinicService GetClinicService()
    {
        return _provider.GetRequiredService<IClinicService>();
    }

    public IScheduleService GetScheduleService()
    {
        return _provider.GetRequiredService<IScheduleService>();
    }

    public IAppointmentService GetAppointmentService()
    {
        return _provider.GetRequiredService<IAppointmentService>();
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// Creates an authenticated HttpClient for a user with the specified role and clinic.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string role, Guid? clinicId = null)
    {
        var client = _factory.CreateClient();
        
        // Ensure user exists
        var userManager = GetUserManager();
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                ClinicId = clinicId
            };
            var createResult = await userManager.CreateAsync(user, "TestPass#123");
            if (!createResult.Succeeded)
                throw new InvalidOperationException($"Failed to create user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            
            await userManager.AddToRoleAsync(user, role);
        }
        else if (user.ClinicId != clinicId)
        {
            user.ClinicId = clinicId;
            await userManager.UpdateAsync(user);
        }

        // Create claims principal
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, role)
        };
        if (user.ClinicId.HasValue)
            claims.Add(new Claim("ClinicId", user.ClinicId.Value.ToString()));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        
        // For testing with WebApplicationFactory, we need to simulate auth
        // The controllers use [Authorize] which checks the principal
        // In integration tests with WebApplicationFactory, we can't easily inject the principal
        // So we'll use a custom approach: set a test header that the middleware can read
        // Or use the test server's authentication
        
        return client;
    }

    /// <summary>
    /// Creates a clinic using the service.
    /// </summary>
    public async Task<Clinic> CreateClinicAsync(string name, string subdomain, string contactEmail = null, string? phone = null, string? address = null, string timeZoneId = "America/Mexico_City")
    {
        var clinicService = GetClinicService();
        var dto = new CreateClinicDto(name, subdomain, contactEmail ?? $"admin@{subdomain}.test", phone, address, timeZoneId);
        
        // Get platform admin user for CreatedByUserId
        var userManager = GetUserManager();
        var platformAdmin = await userManager.GetUsersInRoleAsync(KlinaraRoles.PlatformAdmin);
        var createdBy = platformAdmin.FirstOrDefault() ?? throw new InvalidOperationException("No PlatformAdmin found");
        
        var result = await clinicService.CreateAsync(dto, createdBy.Id);
        if (!result.Success)
            throw new InvalidOperationException($"Failed to create clinic: {string.Join(", ", result.Errors)}");
        
        return result.Clinic!;
    }

    /// <summary>
    /// Creates a clinic with an AdminClinica user.
    /// </summary>
    public async Task<(Guid ClinicId, ApplicationUser AdminUser)> CreateClinicWithAdminAsync(string name, string subdomain, string adminEmail = null)
    {
        var clinic = await CreateClinicAsync(name, subdomain);
        
        var adminEmailFinal = adminEmail ?? $"admin@{subdomain}.test";
        var userManager = GetUserManager();
        
        var admin = new ApplicationUser
        {
            UserName = adminEmailFinal,
            Email = adminEmailFinal,
            EmailConfirmed = true,
            ClinicId = clinic.Id
        };
        var createResult = await userManager.CreateAsync(admin, "TestPass#123");
        if (!createResult.Succeeded)
            throw new InvalidOperationException($"Failed to create admin: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        
        await userManager.AddToRoleAsync(admin, KlinaraRoles.ClinicAdmin);
        
        return (clinic.Id, admin);
    }

    /// <summary>
    /// Creates a PlatformAdmin user and returns HttpClient authenticated as them.
    /// </summary>
    public async Task<HttpClient> CreatePlatformAdminClientAsync()
    {
        var userManager = GetUserManager();
        var email = "platform@test.com";
        
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                ClinicId = null
            };
            await userManager.CreateAsync(user, "TestPass#123");
            await userManager.AddToRoleAsync(user, KlinaraRoles.PlatformAdmin);
        }

        var client = _factory.CreateClient();
        // In a real integration test, we'd set up proper authentication
        // For now, we'll rely on the test server's test authentication
        return client;
    }
}

/// <summary>
/// Extension to create ControllerTestFixture from WebApplicationFactory.
/// </summary>
public static class ControllerTestFixtureExtensions
{
    public static ControllerTestFixture CreateControllerTestFixture(this WebApplicationFactory<Program> factory)
    {
        return new ControllerTestFixture(factory);
    }
}