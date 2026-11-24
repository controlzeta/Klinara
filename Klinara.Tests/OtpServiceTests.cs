using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Klinara.Tests;

public class OtpServiceTests
{
    [Fact]
    public void OtpPurposes_Constants_Are_Defined()
    {
        Assert.Equal("LOGIN", OtpPurposes.Login);
        Assert.Equal("EMAIL_VERIFICATION", OtpPurposes.EmailVerification);
        Assert.Equal("PASSWORD_RESET", OtpPurposes.PasswordReset);
        Assert.Equal("PATIENT_PORTAL", OtpPurposes.PatientPortalAccess);
    }

    [Fact]
    public async Task ValidateOtpAsync_PassesSixDigitCodeUnmodifiedToIdentityProvider()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        string? receivedToken = null;

        userManager
            .Setup(manager => manager.VerifyUserTokenAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string, string, string>((_, _, _, token) => receivedToken = token)
            .ReturnsAsync(true);

        var service = new OtpService(userManager.Object, new ConfigurationBuilder().Build());
        var isValid = await service.ValidateOtpAsync(new ApplicationUser(), "123456", OtpPurposes.EmailVerification);

        Assert.True(isValid);
        Assert.Equal("123456", receivedToken);
    }
}