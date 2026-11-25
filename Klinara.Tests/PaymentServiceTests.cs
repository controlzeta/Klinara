using System;
using System.Linq;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Klinara.Tests;

public class PaymentServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task RegisterPaymentAsync_CalculatesBalanceAndAddsPayment()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var clinicId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var patient = new Patient
        {
            Id = patientId,
            ClinicId = clinicId,
            FullName = "Carlos Santana",
            TotalDue = 1500m,
            CreatedByUserId = userId
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var service = new PaymentService(context, NullLogger<PaymentService>.Instance);

        // Act - Registrar primer abono
        var dto1 = new RegisterPaymentDto(
            clinicId,
            patientId,
            null,
            500m,
            PaymentMethod.Cash,
            "Abono inicial",
            "Recibo #001",
            DateTime.UtcNow
        );
        var result1 = await service.RegisterPaymentAsync(dto1, userId);

        // Act - Registrar segundo abono
        var dto2 = new RegisterPaymentDto(
            clinicId,
            patientId,
            null,
            400m,
            PaymentMethod.CreditCard,
            "Segundo abono",
            "Voucher #99",
            DateTime.UtcNow
        );
        var result2 = await service.RegisterPaymentAsync(dto2, userId);

        var balance = await service.GetPatientBalanceAsync(patientId);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Equal(1500m, balance.TotalDue);
        Assert.Equal(900m, balance.TotalPaid);
        Assert.Equal(600m, balance.Balance);
        Assert.Equal(2, balance.Payments.Count);
    }

    [Fact]
    public async Task RegisterPaymentAsync_RejectsInvalidAmounts()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PaymentService(context, NullLogger<PaymentService>.Instance);

        var dto = new RegisterPaymentDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            0m,
            PaymentMethod.Cash,
            "Concepto",
            null,
            DateTime.UtcNow
        );

        // Act
        var result = await service.RegisterPaymentAsync(dto, Guid.NewGuid());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("mayor a cero", result.Error);
    }
}
