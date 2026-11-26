using System;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Klinara.Tests;

public class ClinicServiceTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ClinicService CreateService(ApplicationDbContext context)
    {
        var loggerFactory = new LoggerFactory();
        var logger = loggerFactory.CreateLogger<ClinicService>();
        return new ClinicService(context, logger);
    }

    // ==========================================
    // CREATE CLINIC
    // ==========================================

    [Fact]
    public async Task CreateAsync_ValidDto_CreatesClinic()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAsync_ValidDto_CreatesClinic));
        var service = CreateService(context);
        var dto = new CreateClinicDto(
            Name: "Clínica Test",
            Subdomain: "clinica-test",
            ContactEmail: "admin@clinica-test.com",
            Phone: "555-1234",
            Address: "Av. Test 123",
            TimeZoneId: "America/Mexico_City"
        );

        // Act
        var result = await service.CreateAsync(dto, Guid.NewGuid());

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Clinic);
        Assert.Equal("Clínica Test", result.Clinic.Name);
        Assert.Equal("clinica-test", result.Clinic.Subdomain);
        Assert.Equal("admin@clinica-test.com", result.Clinic.ContactEmail);
        Assert.True(result.Clinic.IsActive);
    }

    [Fact]
    public async Task CreateAsync_DuplicateSubdomain_ReturnsFailure()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAsync_DuplicateSubdomain_ReturnsFailure));
        var service = CreateService(context);
        var dto1 = new CreateClinicDto("Clínica 1", "misma-subdomain", "a@a.com", "111", "Dir 1", "America/Mexico_City");
        var dto2 = new CreateClinicDto("Clínica 2", "misma-subdomain", "b@b.com", "222", "Dir 2", "America/Mexico_City");
        await service.CreateAsync(dto1, Guid.NewGuid());

        // Act
        var result = await service.CreateAsync(dto2, Guid.NewGuid());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("subdominio", result.Errors[0].ToLower());
    }

    [Fact]
    public async Task CreateAsync_DuplicateContactEmail_ReturnsFailure()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAsync_DuplicateContactEmail_ReturnsFailure));
        var service = CreateService(context);
        var dto1 = new CreateClinicDto("Clínica 1", "sub-1", "shared@test.com", "111", "Dir 1", "America/Mexico_City");
        var dto2 = new CreateClinicDto("Clínica 2", "sub-2", "shared@test.com", "222", "Dir 2", "America/Mexico_City");
        await service.CreateAsync(dto1, Guid.NewGuid());

        // Act
        var result = await service.CreateAsync(dto2, Guid.NewGuid());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("correo", result.Errors[0].ToLower());
    }

    [Fact]
    public async Task CreateAsync_InvalidTimeZoneId_ReturnsFailure()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAsync_InvalidTimeZoneId_ReturnsFailure));
        var service = CreateService(context);
        var dto = new CreateClinicDto("Clínica", "subdomain", "a@a.com", "111", "Dir", "Invalid/TimeZone");

        // Act
        var result = await service.CreateAsync(dto, Guid.NewGuid());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("zona horaria", result.Errors[0].ToLower());
    }

    [Fact]
    public async Task CreateAsync_SubdomainNormalizedToLowercase()
    {
        // Arrange
        await using var context = CreateContext(nameof(CreateAsync_SubdomainNormalizedToLowercase));
        var service = CreateService(context);
        var dto = new CreateClinicDto("Clínica", "CLINICA-TEST", "a@a.com", "111", "Dir", "America/Mexico_City");

        // Act
        var result = await service.CreateAsync(dto, Guid.NewGuid());

        // Assert
        Assert.True(result.Success);
        Assert.Equal("clinica-test", result.Clinic.Subdomain);
    }

    // ==========================================
    // GET CLINIC
    // ==========================================

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsClinic()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetByIdAsync_Existing_ReturnsClinic));
        var service = CreateService(context);
        var created = await service.CreateAsync(
            new CreateClinicDto("Clínica Test", "clinica-test", "test@test.com", null, null),
            Guid.NewGuid());

        // Act
        var clinic = await service.GetByIdAsync(created.Clinic!.Id);

        // Assert
        Assert.NotNull(clinic);
        Assert.Equal("Clínica Test", clinic.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetByIdAsync_NotFound_ReturnsNull));
        var service = CreateService(context);

        // Act
        var clinic = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(clinic);
    }

    [Fact]
    public async Task GetBySubdomainAsync_Existing_ReturnsClinic()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetBySubdomainAsync_Existing_ReturnsClinic));
        var service = CreateService(context);
        await service.CreateAsync(
            new CreateClinicDto("Clínica Test", "clinica-test", "test@test.com", null, null),
            Guid.NewGuid());

        // Act
        var clinic = await service.GetBySubdomainAsync("clinica-test");

        // Assert
        Assert.NotNull(clinic);
        Assert.Equal("clinica-test", clinic.Subdomain);
    }

    [Fact]
    public async Task GetBySubdomainAsync_CaseInsensitive()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetBySubdomainAsync_CaseInsensitive));
        var service = CreateService(context);
        await service.CreateAsync(
            new CreateClinicDto("Clínica Test", "clinica-test", "test@test.com", null, null),
            Guid.NewGuid());

        // Act
        var clinic = await service.GetBySubdomainAsync("CLINICA-TEST");

        // Assert
        Assert.NotNull(clinic);
    }

    [Fact]
    public async Task GetBySubdomainAsync_NotFound_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetBySubdomainAsync_NotFound_ReturnsNull));
        var service = CreateService(context);

        // Act
        var clinic = await service.GetBySubdomainAsync("no-existe");

        // Assert
        Assert.Null(clinic);
    }

    // ==========================================
    // GET ALL (Multi-tenant)
    // ==========================================

    [Fact]
    public async Task GetAllAsync_PlatformAdmin_ReturnsAll()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetAllAsync_PlatformAdmin_ReturnsAll));
        var service = CreateService(context);
        await service.CreateAsync(
            new CreateClinicDto("Clínica A", "clinica-a", "a@a.com", null, null),
            Guid.NewGuid());
        await service.CreateAsync(
            new CreateClinicDto("Clínica B", "clinica-b", "b@b.com", null, null),
            Guid.NewGuid());

        // Act
        var clinics = await service.GetAllAsync(isPlatformAdmin: true);

        // Assert
        Assert.Equal(2, clinics.Count);
    }

    [Fact]
    public async Task GetAllAsync_ClinicAdmin_ReturnsOnlyOwn()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetAllAsync_ClinicAdmin_ReturnsOnlyOwn));
        var service = CreateService(context);
        var clinic1 = await service.CreateAsync(
            new CreateClinicDto("Mi Clínica", "mi-clinica", "a@a.com", null, null),
            Guid.NewGuid());
        await service.CreateAsync(
            new CreateClinicDto("Otra Clínica", "otra-clinica", "b@b.com", null, null),
            Guid.NewGuid());

        // Act - ClinicAdmin solo ve su clínica
        var clinics = await service.GetAllAsync(requestingUserClinicId: clinic1.Clinic!.Id, isPlatformAdmin: false);

        // Assert
        Assert.Single(clinics);
        Assert.Equal("Mi Clínica", clinics[0].Name);
    }

    // ==========================================
    // UPDATE CLINIC
    // ==========================================

    [Fact]
    public async Task UpdateAsync_ValidUpdates_ReturnsSuccess()
    {
        // Arrange
        await using var context = CreateContext(nameof(UpdateAsync_ValidUpdates_ReturnsSuccess));
        var service = CreateService(context);
        var created = await service.CreateAsync(
            new CreateClinicDto("Original", "original", "orig@test.com", null, null),
            Guid.NewGuid());
        var dto = new UpdateClinicDto(
            Name: "Actualizada",
            ContactEmail: "nuevo@test.com",
            Phone: "555-9999",
            Address: "Nueva Dir",
            TimeZoneId: "America/Mexico_City",
            IsActive: true
        );

        // Act
        var result = await service.UpdateAsync(created.Clinic!.Id, dto, Guid.NewGuid(), isPlatformAdmin: true);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Actualizada", result.Clinic!.Name);
        Assert.Equal("nuevo@test.com", result.Clinic.ContactEmail);
    }

    [Fact]
    public async Task UpdateAsync_CannotChangeSubdomain()
    {
        // Arrange
        await using var context = CreateContext(nameof(UpdateAsync_CannotChangeSubdomain));
        var service = CreateService(context);
        var created = await service.CreateAsync(
            new CreateClinicDto("Test", "test-sub", "test@test.com", null, null),
            Guid.NewGuid());
        var dto = new UpdateClinicDto(
            Name: "Test",
            ContactEmail: "test@test.com",
            Phone: null,
            Address: null,
            TimeZoneId: "America/Mexico_City",
            IsActive: true
        );

        // Act
        var result = await service.UpdateAsync(created.Clinic!.Id, dto, Guid.NewGuid(), isPlatformAdmin: true);

        // Assert - Subdomain no está en DTO, no debe cambiar
        Assert.True(result.Success);
        Assert.Equal("test-sub", result.Clinic!.Subdomain);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateContactEmail_ReturnsFailure()
    {
        // Arrange
        await using var context = CreateContext(nameof(UpdateAsync_DuplicateContactEmail_ReturnsFailure));
        var service = CreateService(context);
        var clinic1Result = await service.CreateAsync(
            new CreateClinicDto("Clínica 1", "clinica-1", "shared@test.com", null, null),
            Guid.NewGuid());
        var clinic2Result = await service.CreateAsync(
            new CreateClinicDto("Clínica 2", "clinica-2", "other@test.com", null, null),
            Guid.NewGuid());
        
        // Debug: verify clinics created correctly
        var clinic1 = clinic1Result.Clinic!;
        var clinic2 = clinic2Result.Clinic!;
        Assert.Equal("shared@test.com", clinic1.ContactEmail);
        Assert.Equal("other@test.com", clinic2.ContactEmail);
        
        var dto = new UpdateClinicDto(
            Name: "Clínica 2",
            ContactEmail: "shared@test.com", // Duplicate
            Phone: null,
            Address: null,
            TimeZoneId: "America/Mexico_City",
            IsActive: true
        );

        // Debug: verify clinic2 ID
        var clinic2Id = clinic2.Id;

        // Act - Intentar actualizar clínica 2 con el email de clínica 1
        var result = await service.UpdateAsync(clinic2.Id, dto, Guid.NewGuid(), isPlatformAdmin: true);

        // Debug: print result
        System.Console.WriteLine($"result.Success = {result.Success}");
        if (!result.Success)
        {
            System.Console.WriteLine($"Errors: {string.Join(", ", result.Errors)}");
        }

        // Also check what the database has
        var clinicFromDb = await context.Clinics.FindAsync(clinic2.Id);
        System.Console.WriteLine($"clinicFromDb.ContactEmail = {clinicFromDb?.ContactEmail}");

        // Also check if clinic1 exists in DB
        var clinic1FromDb = await context.Clinics.FindAsync(clinic1.Id);
        System.Console.WriteLine($"clinic1FromDb.ContactEmail = {clinic1FromDb?.ContactEmail}");

        // Also check all clinics in DB
        var allClinics = await context.Clinics.ToListAsync();
        foreach (var c in allClinics)
        {
            System.Console.WriteLine($"DB Clinic: Id={c.Id}, Email={c.ContactEmail}, Subdomain={c.Subdomain}");
        }

        // Assert
        Assert.False(result.Success);
        Assert.Contains("correo", result.Errors[0].ToLower());
    }

    // ==========================================
    // SET ACTIVE (PlatformAdmin only)
    // ==========================================

    [Fact]
    public async Task SetActiveAsync_PlatformAdmin_TogglesIsActive()
    {
        // Arrange
        await using var context = CreateContext(nameof(SetActiveAsync_PlatformAdmin_TogglesIsActive));
        var service = CreateService(context);
        var created = await service.CreateAsync(
            new CreateClinicDto("Para Activar", "para-activar", "act@test.com", null, null),
            Guid.NewGuid());

        // Act - Desactivar
        var result = await service.SetActiveAsync(created.Clinic!.Id, false, Guid.NewGuid(), isPlatformAdmin: true);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.Clinic!.IsActive);

        // Act - Reactivar
        var result2 = await service.SetActiveAsync(created.Clinic.Id, true, Guid.NewGuid(), isPlatformAdmin: true);
        Assert.True(result2.Success);
        Assert.True(result2.Clinic!.IsActive);
    }

    [Fact]
    public async Task SetActiveAsync_NonPlatformAdmin_ReturnsFailure()
    {
        // Arrange
        await using var context = CreateContext(nameof(SetActiveAsync_NonPlatformAdmin_ReturnsFailure));
        var service = CreateService(context);
        var created = await service.CreateAsync(
            new CreateClinicDto("Test", "test", "test@test.com", null, null),
            Guid.NewGuid());

        // Act
        var result = await service.SetActiveAsync(created.Clinic!.Id, false, Guid.NewGuid(), isPlatformAdmin: false);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("plataforma", result.Errors[0].ToLower());
    }
}