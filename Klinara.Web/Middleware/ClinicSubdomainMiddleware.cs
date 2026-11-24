using System;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Klinara.Web.Middleware;

public class ClinicSubdomainMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ClinicSubdomainMiddleware> _logger;

    public ClinicSubdomainMiddleware(RequestDelegate next, ILogger<ClinicSubdomainMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Solo procesar rutas del portal de pacientes
        if (!context.Request.Path.StartsWithSegments("/api/patient-portal") &&
            !context.Request.Path.StartsWithSegments("/portal"))
        {
            await _next(context);
            return;
        }

        var subdomain = ExtractSubdomain(context.Request);

        if (!string.IsNullOrEmpty(subdomain))
        {
            // Resolver ClinicId y guardarlo en HttpContext.Items para uso posterior
            using var scope = context.RequestServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var clinic = await dbContext.Clinics
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Subdomain == subdomain && c.IsActive);

            if (clinic != null)
            {
                context.Items["ClinicId"] = clinic.Id;
                context.Items["ClinicSubdomain"] = clinic.Subdomain;
                context.Items["ClinicTimeZoneId"] = clinic.TimeZoneId;
                _logger.LogDebug("Subdominio resuelto: {Subdomain} -> ClinicId: {ClinicId}", subdomain, clinic.Id);
            }
            else
            {
                _logger.LogWarning("Subdominio no encontrado o inactivo: {Subdomain}", subdomain);
                // No retornamos 404 aquí, dejamos que el controller maneje el caso
            }
        }

        await _next(context);
    }

    private static string? ExtractSubdomain(HttpRequest request)
    {
        var host = request.Host.Host;

        // Desarrollo local: localhost:5000/{subdomain} o {subdomain}.localhost
        if (host.Contains("localhost") || host.Contains("127.0.0.1"))
        {
            // Intentar extraer de la ruta: /portal/{subdomain}/... o /api/patient-portal/clinics/{subdomain}/...
            var path = request.Path.Value?.TrimStart('/');
            if (!string.IsNullOrEmpty(path))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                // Buscar patrón: portal/{subdomain} o patient-portal/clinics/{subdomain}
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    if ((segments[i] == "portal" || segments[i] == "patient-portal") &&
                        (i + 1 < segments.Length))
                    {
                        // Para /patient-portal/clinics/{subdomain}/...
                        if (segments[i] == "patient-portal" && i + 2 < segments.Length && segments[i + 1] == "clinics")
                        {
                            return segments[i + 2];
                        }
                        // Para /portal/{subdomain}/...
                        if (segments[i] == "portal")
                        {
                            return segments[i + 1];
                        }
                    }
                }
            }

            // Fallback: header personalizado para testing
            if (request.Headers.TryGetValue("X-Clinic-Subdomain", out var headerSubdomain))
                return headerSubdomain.ToString();

            return null;
        }

        // Producción: {subdomain}.klinara.com
        var parts = host.Split('.');
        if (parts.Length >= 3) // subdomain.domain.tld
        {
            return parts[0];
        }

        return null;
    }
}

public static class ClinicSubdomainMiddlewareExtensions
{
    public static IApplicationBuilder UseClinicSubdomain(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ClinicSubdomainMiddleware>();
    }
}