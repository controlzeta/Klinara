using Klinara.Web.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Klinara.Web.Data;

public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in KlinaraRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        // Seed demo data
        await SeedDemoDataAsync(scope.ServiceProvider);
    }

    private static async Task SeedDemoDataAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // 1. Platform Admin: controlzetapuebla@gmail.com
        const string platformAdminEmail = "controlzetapuebla@gmail.com";
        var platformAdmin = await userManager.FindByEmailAsync(platformAdminEmail);
        if (platformAdmin == null)
        {
            platformAdmin = new ApplicationUser
            {
                UserName = platformAdminEmail,
                Email = platformAdminEmail,
                EmailConfirmed = true,
                ClinicId = null, // Platform admin has no clinic
                MustChangePassword = false
            };
            var result = await userManager.CreateAsync(platformAdmin, "Password.321");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(platformAdmin, KlinaraRoles.PlatformAdmin);
            }
        }

        // 2. Clinic: Clinica de Prueba
        var clinic = await context.Clinics.FirstOrDefaultAsync(c => c.Name == "Clinica de Prueba");
        if (clinic == null)
        {
            clinic = new Clinic
            {
                Name = "Clinica de Prueba",
                Subdomain = "clinica-prueba",
                Description = "Clínica de prueba para desarrollo",
                ContactEmail = "clinica@prueba.com",
                Phone = "2221234567",
                Address = "Av. Principal 123, Puebla",
                TimeZoneId = "America/Mexico_City",
                IsActive = true
            };
            context.Clinics.Add(clinic);
            await context.SaveChangesAsync();
        }

        // 3. Doctor: Doctor Simi in Clinica de Prueba
        const string doctorEmail = "doctor.simi@clinica-prueba.com";
        var doctor = await userManager.FindByEmailAsync(doctorEmail);
        if (doctor == null)
        {
            doctor = new ApplicationUser
            {
                UserName = doctorEmail,
                Email = doctorEmail,
                EmailConfirmed = true,
                ClinicId = clinic.Id,
                MustChangePassword = false
            };
            var result = await userManager.CreateAsync(doctor, "Password.321");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(doctor, KlinaraRoles.Doctor);
            }
        }

        // 4. Employee: Empleadito in Clinica de Prueba
        const string employeeEmail = "empleadito@clinica-prueba.com";
        var employee = await userManager.FindByEmailAsync(employeeEmail);
        if (employee == null)
        {
            employee = new ApplicationUser
            {
                UserName = employeeEmail,
                Email = employeeEmail,
                EmailConfirmed = true,
                ClinicId = clinic.Id,
                MustChangePassword = false
            };
            var result = await userManager.CreateAsync(employee, "Password.321");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(employee, KlinaraRoles.Employee);
            }
        }
    }
}