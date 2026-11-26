using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit;

namespace Klinara.E2E;

public class DashboardScreenshotTests : PageTest
{
    [Fact]
    public async Task LandingPage_CapturesScreenshot()
    {
        await Page.GotoAsync("http://localhost:5247/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = "screenshots/01_landing_page.png",
            FullPage = true
        });
        
        await Expect(Page.Locator("h1")).ToContainTextAsync("Bienvenido a Klinara");
        await Expect(Page.Locator("a:has-text('Iniciar Sesión')")).ToBeVisibleAsync();
    }
    
    [Fact]
    public async Task LoginPage_CapturesScreenshot()
    {
        await Page.GotoAsync("http://localhost:5247/Account/Login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = "screenshots/02_login_page.png",
            FullPage = true
        });
        
        await Expect(Page.Locator("input[name='Input.Email']")).ToBeVisibleAsync();
        await Expect(Page.Locator("button[type='submit']")).ToBeVisibleAsync();
    }
    
    [Fact]
    public async Task RegisterPage_CapturesScreenshot()
    {
        await Page.GotoAsync("http://localhost:5247/Account/Register");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = "screenshots/03_register_page.png",
            FullPage = true
        });
        
        await Expect(Page.Locator("input[name='Input.Email']")).ToBeVisibleAsync();
    }
    
    [Fact]
    public async Task PatientPortalLanding_CapturesScreenshot()
    {
        await Page.GotoAsync("http://localhost:5247/portal/demo");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = "screenshots/04_patient_portal_landing.png",
            FullPage = true
        });
        
        await Expect(Page.Locator("h1:has-text('Portal del Paciente')")).ToBeVisibleAsync();
    }
    
    [Fact]
    public async Task PatientPortalBooking_CapturesScreenshot()
    {
        await Page.GotoAsync("http://localhost:5247/portal/demo/booking");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = "screenshots/05_patient_portal_booking.png",
            FullPage = true
        });
    }
    
    [Fact]
    public async Task PatientPortalVerify_CapturesScreenshot()
    {
        await Page.GotoAsync("http://localhost:5247/portal/demo/verify");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = "screenshots/06_patient_portal_verify.png",
            FullPage = true
        });
    }
    
    [Fact]
    public async Task DashboardRedirect_CapturesScreenshot()
    {
        await Page.GotoAsync("http://localhost:5247/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = "screenshots/07_dashboard_redirect.png",
            FullPage = true
        });
        
        Assert.Contains("/Account/Login", Page.Url);
    }
}