using System;
using System.Threading.Tasks;
using Klinara.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Klinara_Test;Trusted_Connection=True;TrustServerCertificate=True"));

using var host = builder.Build();

using var scope = host.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

Console.WriteLine("Eliminando base de datos existente...");
await context.Database.EnsureDeletedAsync();

Console.WriteLine("Creando base de datos desde el modelo actual...");
await context.Database.EnsureCreatedAsync();

Console.WriteLine("✅ Base de datos Klinara_Test creada exitosamente en (localdb)\\MSSQLLocalDB");

// Verificar tablas creadas
var tables = await context.Database.SqlQueryRaw<string>(
    "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME")
    .ToListAsync();

Console.WriteLine($"\nTablas creadas ({tables.Count}):");
foreach (var t in tables)
    Console.WriteLine($"  - {t}");