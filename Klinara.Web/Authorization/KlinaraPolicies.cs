using Microsoft.AspNetCore.Authorization;
using Klinara.Web.Authorization;

namespace Klinara.Web.Authorization;

public static class KlinaraPolicies
{
    public const string PlatformAdminOnly = "PlatformAdminOnly";
    public const string ClinicAdminOrAbove = "ClinicAdminOrAbove";
    public const string DoctorOrAbove = "DoctorOrAbove";
    public const string EmployeeOrAbove = "EmployeeOrAbove";
    public const string SameClinicOnly = "SameClinicOnly";
    public const string CanManageUsers = "CanManageUsers";
    public const string CanManageAppointments = "CanManageAppointments";
    public const string CanManagePayments = "CanManagePayments";
    public const string CanUnblockPatients = "CanUnblockPatients";
    public const string CanViewAllClinics = "CanViewAllClinics";

    public static void Configure(AuthorizationOptions options)
    {
        // Roles de plataforma (acceso global)
        options.AddPolicy(PlatformAdminOnly, policy =>
            policy.RequireRole(KlinaraRoles.PlatformAdmin));

        options.AddPolicy(CanViewAllClinics, policy =>
            policy.RequireRole(KlinaraRoles.PlatformAdmin));

        // Roles de clínica (alcance por clínica)
        options.AddPolicy(ClinicAdminOrAbove, policy =>
            policy.RequireRole(KlinaraRoles.ClinicAdmin, KlinaraRoles.PlatformAdmin));

        options.AddPolicy(DoctorOrAbove, policy =>
            policy.RequireRole(KlinaraRoles.Doctor, KlinaraRoles.ClinicAdmin, KlinaraRoles.PlatformAdmin));

        options.AddPolicy(EmployeeOrAbove, policy =>
            policy.RequireRole(KlinaraRoles.Employee, KlinaraRoles.Doctor, KlinaraRoles.ClinicAdmin, KlinaraRoles.PlatformAdmin));

        // Políticas de permisos funcionales
        options.AddPolicy(CanManageUsers, policy =>
            policy.RequireRole(KlinaraRoles.ClinicAdmin, KlinaraRoles.PlatformAdmin));

        options.AddPolicy(CanManageAppointments, policy =>
            policy.RequireRole(KlinaraRoles.Employee, KlinaraRoles.Doctor, KlinaraRoles.ClinicAdmin, KlinaraRoles.PlatformAdmin));

        options.AddPolicy(CanManagePayments, policy =>
            policy.RequireRole(KlinaraRoles.Employee, KlinaraRoles.Doctor, KlinaraRoles.ClinicAdmin, KlinaraRoles.PlatformAdmin));

        options.AddPolicy(CanUnblockPatients, policy =>
            policy.RequireRole(KlinaraRoles.Employee, KlinaraRoles.Doctor, KlinaraRoles.ClinicAdmin, KlinaraRoles.PlatformAdmin));

        // Política para asegurar que el usuario pertenece a la clínica (o es PlatformAdmin)
        options.AddPolicy(SameClinicOnly, policy =>
        {
            policy.RequireAssertion(context =>
            {
                var user = context.User;
                if (user.IsInRole(KlinaraRoles.PlatformAdmin))
                    return true;

                var clinicIdClaim = user.FindFirst("ClinicId");
                return clinicIdClaim != null && Guid.TryParse(clinicIdClaim.Value, out _);
            });
        });
    }
}