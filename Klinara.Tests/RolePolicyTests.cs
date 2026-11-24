using System.Linq;
using System.Security.Claims;
using Klinara.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Xunit;

namespace Klinara.Tests;

public class RolePolicyTests
{
    private static AuthorizationOptions CreateOptions()
    {
        var options = new AuthorizationOptions();
        KlinaraPolicies.Configure(options);
        return options;
    }

    private static ClaimsPrincipal CreateUser(params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.type, c.value)), "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void KlinaraRoles_Constants_Are_Defined()
    {
        Assert.Equal("AdminPlataforma", KlinaraRoles.PlatformAdmin);
        Assert.Equal("AdminClinica", KlinaraRoles.ClinicAdmin);
        Assert.Equal("Doctor", KlinaraRoles.Doctor);
        Assert.Equal("Empleado", KlinaraRoles.Employee);

        Assert.Equal(4, KlinaraRoles.All.Count);
        Assert.Equal(3, KlinaraRoles.ClinicScoped.Count);
        Assert.Contains(KlinaraRoles.PlatformAdmin, KlinaraRoles.All);
        Assert.DoesNotContain(KlinaraRoles.PlatformAdmin, KlinaraRoles.ClinicScoped);
    }

    [Fact]
    public void Policies_Are_Registered()
    {
        var options = CreateOptions();

        Assert.NotNull(options.GetPolicy(KlinaraPolicies.PlatformAdminOnly));
        Assert.NotNull(options.GetPolicy(KlinaraPolicies.ClinicAdminOrAbove));
        Assert.NotNull(options.GetPolicy(KlinaraPolicies.DoctorOrAbove));
        Assert.NotNull(options.GetPolicy(KlinaraPolicies.EmployeeOrAbove));
        Assert.NotNull(options.GetPolicy(KlinaraPolicies.SameClinicOnly));
        Assert.NotNull(options.GetPolicy(KlinaraPolicies.CanManageUsers));
        Assert.NotNull(options.GetPolicy(KlinaraPolicies.CanManageAppointments));
        Assert.NotNull(options.GetPolicy(KlinaraPolicies.CanManagePayments));
        Assert.NotNull(options.GetPolicy(KlinaraPolicies.CanUnblockPatients));
        Assert.NotNull(options.GetPolicy(KlinaraPolicies.CanViewAllClinics));
    }

    [Fact]
    public void PlatformAdminOnly_Policy_Requires_PlatformAdmin_Role()
    {
        var options = CreateOptions();
        var policy = options.GetPolicy(KlinaraPolicies.PlatformAdminOnly);
        Assert.NotNull(policy);

        var requirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().FirstOrDefault();
        Assert.NotNull(requirement);
        Assert.Contains(KlinaraRoles.PlatformAdmin, requirement!.AllowedRoles);
        Assert.Single(requirement!.AllowedRoles);
    }

    [Fact]
    public void ClinicAdminOrAbove_Includes_PlatformAdmin_And_ClinicAdmin()
    {
        var options = CreateOptions();
        var policy = options.GetPolicy(KlinaraPolicies.ClinicAdminOrAbove);
        var requirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().FirstOrDefault();
        Assert.NotNull(requirement);
        Assert.Contains(KlinaraRoles.PlatformAdmin, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.ClinicAdmin, requirement!.AllowedRoles);
        Assert.DoesNotContain(KlinaraRoles.Doctor, requirement!.AllowedRoles);
        Assert.DoesNotContain(KlinaraRoles.Employee, requirement!.AllowedRoles);
    }

    [Fact]
    public void DoctorOrAbove_Includes_Doctor_ClinicAdmin_PlatformAdmin()
    {
        var options = CreateOptions();
        var policy = options.GetPolicy(KlinaraPolicies.DoctorOrAbove);
        var requirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().FirstOrDefault();
        Assert.NotNull(requirement);
        Assert.Contains(KlinaraRoles.PlatformAdmin, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.ClinicAdmin, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.Doctor, requirement!.AllowedRoles);
        Assert.DoesNotContain(KlinaraRoles.Employee, requirement!.AllowedRoles);
    }

    [Fact]
    public void EmployeeOrAbove_Includes_All_Roles()
    {
        var options = CreateOptions();
        var policy = options.GetPolicy(KlinaraPolicies.EmployeeOrAbove);
        var requirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().FirstOrDefault();
        Assert.NotNull(requirement);
        foreach (var role in KlinaraRoles.All)
        {
            Assert.Contains(role, requirement!.AllowedRoles);
        }
    }

    [Fact]
    public void CanManageUsers_Only_ClinicAdmin_And_PlatformAdmin()
    {
        var options = CreateOptions();
        var policy = options.GetPolicy(KlinaraPolicies.CanManageUsers);
        var requirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().FirstOrDefault();
        Assert.NotNull(requirement);
        Assert.Contains(KlinaraRoles.PlatformAdmin, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.ClinicAdmin, requirement!.AllowedRoles);
        Assert.DoesNotContain(KlinaraRoles.Doctor, requirement!.AllowedRoles);
        Assert.DoesNotContain(KlinaraRoles.Employee, requirement!.AllowedRoles);
    }

    [Fact]
    public void CanManageAppointments_All_Clinic_Scoped_Roles()
    {
        var options = CreateOptions();
        var policy = options.GetPolicy(KlinaraPolicies.CanManageAppointments);
        var requirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().FirstOrDefault();
        Assert.NotNull(requirement);
        Assert.Contains(KlinaraRoles.PlatformAdmin, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.ClinicAdmin, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.Doctor, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.Employee, requirement!.AllowedRoles);
    }

    [Fact]
    public void CanManagePayments_All_Clinic_Scoped_Roles()
    {
        var options = CreateOptions();
        var policy = options.GetPolicy(KlinaraPolicies.CanManagePayments);
        var requirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().FirstOrDefault();
        Assert.NotNull(requirement);
        Assert.Contains(KlinaraRoles.PlatformAdmin, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.ClinicAdmin, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.Doctor, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.Employee, requirement!.AllowedRoles);
    }

    [Fact]
    public void CanUnblockPatients_Employee_And_Above()
    {
        var options = CreateOptions();
        var policy = options.GetPolicy(KlinaraPolicies.CanUnblockPatients);
        var requirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().FirstOrDefault();
        Assert.NotNull(requirement);
        Assert.Contains(KlinaraRoles.Employee, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.Doctor, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.ClinicAdmin, requirement!.AllowedRoles);
        Assert.Contains(KlinaraRoles.PlatformAdmin, requirement!.AllowedRoles);
    }

    [Fact]
    public void SameClinicOnly_Policy_Exists_With_Custom_Requirement()
    {
        var options = CreateOptions();
        var policy = options.GetPolicy(KlinaraPolicies.SameClinicOnly);
        Assert.NotNull(policy);
        Assert.Single(policy.Requirements); // Custom requirement (AssertionRequirement)
    }

    [Fact]
    public void CanViewAllClinics_Only_PlatformAdmin()
    {
        var options = CreateOptions();
        var policy = options.GetPolicy(KlinaraPolicies.CanViewAllClinics);
        var requirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().FirstOrDefault();
        Assert.NotNull(requirement);
        Assert.Contains(KlinaraRoles.PlatformAdmin, requirement!.AllowedRoles);
        Assert.Single(requirement!.AllowedRoles);
    }
}