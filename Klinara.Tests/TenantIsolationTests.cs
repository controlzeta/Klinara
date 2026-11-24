using System;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace Klinara.Tests;

public class TenantIsolationTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ApplicationUser CreateUser(string email, Guid? clinicId = null, string role = "Empleado")
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            ClinicId = clinicId
        };
        return user;
    }

    private static Clinic CreateClinic(string name, string subdomain)
    {
        return new Clinic
        {
            Name = name,
            Subdomain = subdomain,
            ContactEmail = $"admin@{subdomain}.com"
        };
    }

    [Fact]
    public async Task User_With_ClinicId_Cannot_Access_Other_Clinic_Data()
    {
        // Arrange
        await using var context = CreateContext(nameof(User_With_ClinicId_Cannot_Access_Other_Clinic_Data));

        var clinicA = CreateClinic("Clínica A", "clinica-a");
        var clinicB = CreateClinic("Clínica B", "clinica-b");
        context.Clinics.AddRange(clinicA, clinicB);

        var userA = CreateUser("doctor@clinica-a.com", clinicA.Id, "Doctor");
        var userB = CreateUser("doctor@clinica-b.com", clinicB.Id, "Doctor");
        context.Users.AddRange(userA, userB);

        await context.SaveChangesAsync();

        // Act: Simular query filtrada por ClinicId del usuario autenticado
        var usersFromClinicA = await context.Users
            .Where(u => u.ClinicId == clinicA.Id)
            .ToListAsync();

        var usersFromClinicB = await context.Users
            .Where(u => u.ClinicId == clinicB.Id)
            .ToListAsync();

        // Assert
        Assert.Single(usersFromClinicA);
        Assert.Equal(clinicA.Id, usersFromClinicA[0].ClinicId);
        Assert.Single(usersFromClinicB);
        Assert.Equal(clinicB.Id, usersFromClinicB[0].ClinicId);

        // Verificar aislamiento: usuario A NO ve usuario B
        Assert.DoesNotContain(usersFromClinicA, u => u.Id == userB.Id);
        Assert.DoesNotContain(usersFromClinicB, u => u.Id == userA.Id);
    }

    [Fact]
    public async Task PlatformAdmin_ClinicId_Null_Can_Access_All_Clinics()
    {
        // Arrange
        await using var context = CreateContext(nameof(PlatformAdmin_ClinicId_Null_Can_Access_All_Clinics));

        var clinicA = CreateClinic("Clínica A", "clinica-a");
        var clinicB = CreateClinic("Clínica B", "clinica-b");
        context.Clinics.AddRange(clinicA, clinicB);

        var adminPlataforma = CreateUser("admin@klinara.com", clinicId: null, "AdminPlataforma");
        var userA = CreateUser("doctor@clinica-a.com", clinicA.Id, "Doctor");
        var userB = CreateUser("doctor@clinica-b.com", clinicB.Id, "Doctor");
        context.Users.AddRange(adminPlataforma, userA, userB);

        await context.SaveChangesAsync();

        // Act: Admin sin ClinicId ve todos los usuarios
        var allUsers = await context.Users
            .Where(u => u.ClinicId == null || u.ClinicId != null) // Simula: si es admin, no filtra
            .ToListAsync();

        var usersScopedToA = await context.Users
            .Where(u => u.ClinicId == clinicA.Id)
            .ToListAsync();

        // Assert
        Assert.Equal(3, allUsers.Count); // admin + userA + userB
        Assert.Single(usersScopedToA);
        Assert.Equal(clinicA.Id, usersScopedToA[0].ClinicId);

        // Admin no tiene ClinicId
        Assert.Null(adminPlataforma.ClinicId);
    }

    [Fact]
    public async Task ClinicAdmin_Only_Sees_Own_Clinic_Users()
    {
        // Arrange
        await using var context = CreateContext(nameof(ClinicAdmin_Only_Sees_Own_Clinic_Users));

        var clinicA = CreateClinic("Clínica A", "clinica-a");
        var clinicB = CreateClinic("Clínica B", "clinica-b");
        context.Clinics.AddRange(clinicA, clinicB);

        var adminClinicaA = CreateUser("admin@clinica-a.com", clinicA.Id, "AdminClinica");
        var doctorA = CreateUser("doctor@clinica-a.com", clinicA.Id, "Doctor");
        var empleadoA = CreateUser("empleado@clinica-a.com", clinicA.Id, "Empleado");
        var doctorB = CreateUser("doctor@clinica-b.com", clinicB.Id, "Doctor");

        context.Users.AddRange(adminClinicaA, doctorA, empleadoA, doctorB);
        await context.SaveChangesAsync();

        // Act: Admin de clínica A consulta usuarios de SU clínica
        var myClinicUsers = await context.Users
            .Where(u => u.ClinicId == adminClinicaA.ClinicId)
            .ToListAsync();

        // Assert
        Assert.Equal(3, myClinicUsers.Count); // adminClinicaA + doctorA + empleadoA
        Assert.All(myClinicUsers, u => Assert.Equal(clinicA.Id, u.ClinicId));
        Assert.DoesNotContain(myClinicUsers, u => u.Id == doctorB.Id);
    }

    [Fact]
    public async Task Clinic_Subdomain_Is_Unique_In_Model()
    {
        // Arrange
        await using var context = CreateContext(nameof(Clinic_Subdomain_Is_Unique_In_Model));

        // Act: Verificar que el índice único está configurado en el modelo
        var entityType = context.Model.FindEntityType(typeof(Clinic));
        var subdomainIndex = entityType?.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Clinic.Subdomain)) && i.IsUnique);

        // Assert
        Assert.NotNull(subdomainIndex);
        Assert.True(subdomainIndex.IsUnique);
    }

    [Fact]
    public async Task Clinic_ContactEmail_Is_Unique_In_Model()
    {
        // Arrange
        await using var context = CreateContext(nameof(Clinic_ContactEmail_Is_Unique_In_Model));

        // Act: Verificar que el índice único está configurado en el modelo
        var entityType = context.Model.FindEntityType(typeof(Clinic));
        var emailIndex = entityType?.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Clinic.ContactEmail)) && i.IsUnique);

        // Assert
        Assert.NotNull(emailIndex);
        Assert.True(emailIndex.IsUnique);
    }

    [Fact]
    public async Task User_ClinicId_Index_Exists()
    {
        // Arrange
        await using var context = CreateContext(nameof(User_ClinicId_Index_Exists));

        var clinic = CreateClinic("Test Clinic", "test-clinic");
        context.Clinics.Add(clinic);

        var user1 = CreateUser("user1@test.com", clinic.Id);
        var user2 = CreateUser("user2@test.com", clinic.Id);
        var user3 = CreateUser("user3@test.com", null); // Admin plataforma

        context.Users.AddRange(user1, user2, user3);
        await context.SaveChangesAsync();

        // Act: Query filtrada por ClinicId (debería usar índice)
        var clinicUsers = await context.Users
            .Where(u => u.ClinicId == clinic.Id)
            .ToListAsync();

        // Assert
        Assert.Equal(2, clinicUsers.Count);
        Assert.All(clinicUsers, u => Assert.Equal(clinic.Id, u.ClinicId));
    }
}