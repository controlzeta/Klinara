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
}