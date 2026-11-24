namespace Klinara.Web.Authorization;

public static class KlinaraRoles
{
    public const string PlatformAdmin = "AdminPlataforma";
    public const string ClinicAdmin = "AdminClinica";
    public const string Doctor = "Doctor";
    public const string Employee = "Empleado";

    public static readonly IReadOnlyList<string> All = new[]
    {
        PlatformAdmin,
        ClinicAdmin,
        Doctor,
        Employee
    };

    public static readonly IReadOnlyList<string> ClinicScoped = new[]
    {
        ClinicAdmin,
        Doctor,
        Employee
    };
}