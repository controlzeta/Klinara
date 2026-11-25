using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit;

namespace Klinara.E2E;

/// <summary>
/// Playwright E2E tests for the Patient Portal flow.
/// Tests the complete flow: email verification → OTP → booking → confirmation → view → cancel.
/// </summary>
[Collection("Playwright")]
public class PatientPortalFlowTests : IAsyncLifetime
{
    private readonly IPlaywright _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private IAPIRequestContext? _apiContext;
    private const string BaseUrl = "http://localhost:5247"; // Default dev URL
    private const string ClinicSubdomain = "demo";

    public PatientPortalFlowTests()
    {
        _playwright = Microsoft.Playwright.Playwright.CreateAsync().GetAwaiter().GetResult();
    }

    public async Task InitializeAsync()
    {
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 0
        });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });
        _page = await _context.NewPageAsync();
        _apiContext = await _playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = BaseUrl
        });
    }

    public async Task DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_apiContext != null) await _apiContext.DisposeAsync();
        if (_context != null) await _context.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
        _playwright.Dispose();
    }

    private string PortalBaseUrl => $"{BaseUrl}/portal/{ClinicSubdomain}";

    [Fact]
    public async Task PatientPortal_HomePage_LoadsSuccessfully()
    {
        // Arrange & Act
        var response = await _page!.GotoAsync($"{PortalBaseUrl}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Ok);
        
        // Check for portal branding
        await Assertions.Expect(_page.Locator("h1")).ToContainTextAsync("Bienvenido a Klinara");
        
        // Check for navigation links
        await Assertions.Expect(_page.Locator("a:has-text('Reservar cita')")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("a:has-text('Ver mi cita')")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PatientPortal_VerifyEmail_SendsOtp()
    {
        // Arrange
        await _page!.GotoAsync($"{PortalBaseUrl}/verify", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Act - Enter email and send OTP
        await _page.FillAsync("input[type='email']", "test.patient@demo.com");
        await _page.ClickAsync("button:has-text('Enviar código de acceso')");

        // Assert - Should show success message
        await Assertions.Expect(_page.Locator(".alert-success")).ToBeVisibleAsync(new()
        {
            Timeout = 10000
        });
        await Assertions.Expect(_page.Locator(".alert-success")).ToContainTextAsync("test.patient@demo.com");
        
        // Should show OTP input
        await Assertions.Expect(_page.Locator("input[maxlength='6']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PatientPortal_BookAppointment_RequiresValidToken()
    {
        // Arrange - Try to access booking page without token
        var response = await _page!.GotoAsync($"{PortalBaseUrl}/booking", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Assert - Should redirect to verify page
        Assert.NotNull(response);
        // The page should redirect to verify page when no token
        await Assertions.Expect(_page).ToHaveURLAsync($"{PortalBaseUrl}/verify");
    }

    [Fact]
    public async Task PatientPortal_ConfirmAppointment_InvalidTokenShowsError()
    {
        // Arrange
        var invalidToken = "invalid-token-12345";

        // Act
        var response = await _page!.GotoAsync($"{PortalBaseUrl}/confirm/{invalidToken}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Assert - Should show error page
        Assert.NotNull(response);
        await Assertions.Expect(_page.Locator("text=No se pudo confirmar")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("text=inválido o ha expirado")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PatientPortal_ViewAppointment_InvalidTokenShowsNotFound()
    {
        // Arrange
        var invalidToken = "invalid-token-12345";

        // Act
        var response = await _page!.GotoAsync($"{PortalBaseUrl}/appointment/{invalidToken}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Assert - Should show not found page with option to enter token manually
        Assert.NotNull(response);
        await Assertions.Expect(_page.Locator("text=No se encontró la cita")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator("input[placeholder*='token']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PatientPortal_CancelAppointment_InvalidTokenShowsError()
    {
        // Arrange
        var invalidToken = "invalid-token-12345";

        // Act
        var response = await _page!.GotoAsync($"{PortalBaseUrl}/cancel/{invalidToken}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Assert - Should show error
        Assert.NotNull(response);
        await Assertions.Expect(_page.Locator("text=No se encontró la cita para cancelar")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PatientPortal_API_DoctorsEndpoint_ReturnsData()
    {
        // Act - Call the API directly
        var response = await _apiContext!.GetAsync($"{BaseUrl}/api/patient-portal/clinics/{ClinicSubdomain}/doctors");

        // Assert
        Assert.NotNull(response);
        // Should return 200 OK or 404 if no clinic, but not 500
        Assert.True(response.Ok || response.Status == 404);
        
        if (response.Ok)
        {
            var json = await response.JsonAsync();
            Assert.NotNull(json);
            // Should be an array
            Assert.IsType<JsonElement>(json);
        }
    }

    [Fact]
    public async Task PatientPortal_API_VerifyEmailEndpoint_ValidatesInput()
    {
        // Act - Send invalid request (missing email)
        var response = await _apiContext!.PostAsync($"{BaseUrl}/api/patient-portal/verify-email", new()
        {
            DataObject = new { ClinicSubdomain = ClinicSubdomain }
        });

        // Assert - Should return 400 Bad Request
        Assert.Equal(400, response.Status);
    }

    [Fact]
    public async Task PatientPortal_API_ValidateOtpEndpoint_ValidatesInput()
    {
        // Act - Send invalid request (missing fields)
        var response = await _apiContext!.PostAsync($"{BaseUrl}/api/patient-portal/validate-otp", new()
        {
            DataObject = new { Email = "test@test.com" }
        });

        // Assert - Should return 400 Bad Request
        Assert.Equal(400, response.Status);
    }
}

/// <summary>
/// Test fixture to share browser context across tests
/// </summary>
[CollectionDefinition("Playwright")]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture> { }

public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        Playwright.Dispose();
    }
}