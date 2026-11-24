using System.Reflection;
using Microsoft.AspNetCore.Components;
using Klinara.Web.Components.Pages;

namespace Klinara.Tests;

public class TemplateViewCleanupTests
{
    [Fact]
    public void TemplateDemoRoutes_AreNotRegistered()
    {
        var routes = typeof(Home).Assembly
            .GetTypes()
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>())
            .Select(attribute => attribute.Template)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("/counter", routes);
        Assert.DoesNotContain("/weather", routes);
        Assert.DoesNotContain("/auth", routes);
    }
}
