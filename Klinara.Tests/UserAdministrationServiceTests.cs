using System.Security.Claims;
using Klinara.Web.Authorization;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Klinara.Tests;

public class UserAdministrationServiceTests
{
    [Fact]
    public async Task ChangeRoleAsync_PlatformAdminChangesRoleAndClinic()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var clinicA = await fixture.CreateClinicAsync("Clinic A", "clinic-a");
        var clinicB = await fixture.CreateClinicAsync("Clinic B", "clinic-b");
        var platformAdmin = await fixture.CreateUserAsync("platform@example.test", null, KlinaraRoles.PlatformAdmin);
        var target = await fixture.CreateUserAsync("doctor@example.test", clinicA.Id, KlinaraRoles.Employee);
        var originalSecurityStamp = target.SecurityStamp;
        var actor = CreatePrincipal(platformAdmin, KlinaraRoles.PlatformAdmin);

        var result = await fixture.Administration.ChangeRoleAsync(
            actor, target.Id, KlinaraRoles.Doctor, clinicB.Id);

        Assert.True(result.Success, result.Error);
        Assert.Equal(clinicB.Id, target.ClinicId);
        Assert.Equal(new[] { KlinaraRoles.Doctor }, await fixture.UserManager.GetRolesAsync(target));
        Assert.NotEqual(originalSecurityStamp, target.SecurityStamp);
    }

    [Fact]
    public async Task ChangeRoleAsync_ClinicAdminCannotChangeOtherClinic()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var clinicA = await fixture.CreateClinicAsync("Clinic A", "clinic-a");
        var clinicB = await fixture.CreateClinicAsync("Clinic B", "clinic-b");
        var clinicAdmin = await fixture.CreateUserAsync("clinicadmin@example.test", clinicA.Id, KlinaraRoles.ClinicAdmin);
        var target = await fixture.CreateUserAsync("doctor@example.test", clinicB.Id, KlinaraRoles.Employee);
        var actor = CreatePrincipal(clinicAdmin, KlinaraRoles.ClinicAdmin);

        var result = await fixture.Administration.ChangeRoleAsync(
            actor, target.Id, KlinaraRoles.Doctor, clinicA.Id);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("puede", result.Error!);
    }

    [Fact]
    public async Task ChangeRoleAsync_CannotRemoveLastPlatformAdmin()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var platformAdmin1 = await fixture.CreateUserAsync("admin1@example.test", null, KlinaraRoles.PlatformAdmin);
        var target = await fixture.CreateUserAsync("user2@example.test", null, KlinaraRoles.Employee);
        var actor = CreatePrincipal(platformAdmin1, KlinaraRoles.PlatformAdmin);

        var result = await fixture.Administration.ChangeRoleAsync(
            actor, target.Id, KlinaraRoles.Employee, null);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ChangeRoleAsync_EmployeeCannotChangeRoles()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var admin = await fixture.CreateUserAsync("super@example.test", null, KlinaraRoles.PlatformAdmin);
        var target = await fixture.CreateUserAsync("user@example.test", null, KlinaraRoles.Employee);
        var actor = CreatePrincipal(target, KlinaraRoles.Employee);

        var result = await fixture.Administration.ChangeRoleAsync(
            actor, target.Id, KlinaraRoles.Doctor, null);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("no tiene permiso", result.Error!.ToLower());
    }

    [Fact]
    public async Task ListUsersAsync_PlatformAdminSeesAll()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var platformAdmin = await fixture.CreateUserAsync("platform@example.test", null, KlinaraRoles.PlatformAdmin);
        var userA = await fixture.CreateUserAsync("userA@example.test", null, KlinaraRoles.Employee);
        var userB = await fixture.CreateUserAsync("userB@example.test", null, KlinaraRoles.Employee);
        var actor = CreatePrincipal(platformAdmin, KlinaraRoles.PlatformAdmin);

        var users = await fixture.Administration.ListUsersAsync(actor);

        Assert.Contains(users, u => u.Email == userA.Email);
        Assert.Contains(users, u => u.Email == userB.Email);
        Assert.Equal(3, users.Count);
    }

    private static ClaimsPrincipal CreatePrincipal(ApplicationUser user, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        if (user.ClinicId.HasValue)
            claims = claims.Concat(new[] { new Claim("ClinicId", user.ClinicId.Value.ToString()) }).ToArray();

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private TestFixture(ServiceProvider provider)
        {
            _provider = provider;
            UserManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            Administration = provider.GetRequiredService<IUserAdministrationService>();
            DbContext = provider.GetRequiredService<ApplicationDbContext>();
            Configuration = provider.GetService<IConfiguration>();
        }

        public UserManager<ApplicationUser> UserManager { get; }
        public RoleManager<IdentityRole<Guid>> RoleManager { get; }
        public IUserAdministrationService Administration { get; }
        public ApplicationDbContext DbContext { get; }
        public IConfiguration Configuration { get; }

        public static async Task<TestFixture> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddScoped<IUserAdministrationService, UserAdministrationService>();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Klinara:BootstrapPlatformAdminEmail"] = ""
                }).Build());

            var fixture = new TestFixture(services.BuildServiceProvider());
            foreach (var role in KlinaraRoles.All)
            {
                var roleResult = await fixture.RoleManager.CreateAsync(new IdentityRole<Guid>(role));
                Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }

            // Seed: bootstrap platform admin if configured
            var configEmail = fixture.Configuration["Klinara:BootstrapPlatformAdminEmail"];
            if (!string.IsNullOrEmpty(configEmail))
            {
                var existing = await fixture.UserManager.FindByEmailAsync(configEmail);
                if (existing == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = configEmail,
                        Email = configEmail,
                        EmailConfirmed = true,
                        ClinicId = null
                    };
                    await fixture.UserManager.CreateAsync(user, "Klinara.Test#123");
                    await fixture.UserManager.AddToRoleAsync(user, KlinaraRoles.PlatformAdmin);
                }
            }

            return fixture;
        }

        public async Task<Clinic> CreateClinicAsync(string name, string subdomain)
        {
            var clinic = new Clinic
            {
                Name = name,
                Subdomain = subdomain,
                ContactEmail = $"admin@{subdomain}.example.test"
            };
            DbContext.Clinics.Add(clinic);
            await DbContext.SaveChangesAsync();
            return clinic;
        }

        public async Task<ApplicationUser> CreateUserAsync(string email, Guid? clinicId, string role)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                ClinicId = clinicId
            };
            var createResult = await UserManager.CreateAsync(user, "Klinara.Test#123");
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));
            var roleResult = await UserManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}