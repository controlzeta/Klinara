using System.Text;
using Klinara.Web.Data;
using Klinara.Web.Services;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Klinara.Web.Services;

public class AdminExportService : IAdminExportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminExportService> _logger;

    public AdminExportService(ApplicationDbContext context, ILogger<AdminExportService> logger)
    {
        _context = context;
        _logger = logger;
        
        // Configurar licencia EPPlus (no comercial)
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        
        // Configurar QuestPDF
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> ExportPaymentsCsvAsync(Guid clinicId, DateTime? startDate = null, DateTime? endDate = null, Guid? patientId = null)
    {
        var payments = await GetPaymentsQuery(clinicId, startDate, endDate, patientId)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Pagos");

        // Headers
        var headers = new[] { "ID", "Paciente", "Monto", "Método", "Concepto", "Fecha Pago", "Cita" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cells[1, i + 1].Value = headers[i];
            worksheet.Cells[1, i + 1].Style.Font.Bold = true;
        }

        // Data
        for (int i = 0; i < payments.Count; i++)
        {
            var p = payments[i];
            var row = i + 2;
            worksheet.Cells[row, 1].Value = p.Id.ToString();
            worksheet.Cells[row, 2].Value = p.PatientName;
            worksheet.Cells[row, 3].Value = p.Amount;
            worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 4].Value = GetMethodDisplayName(p.Method);
            worksheet.Cells[row, 5].Value = p.Concept;
            worksheet.Cells[row, 6].Value = p.PaidAt;
            worksheet.Cells[row, 6].Style.Numberformat.Format = "yyyy-mm-dd hh:mm";
            worksheet.Cells[row, 7].Value = p.AppointmentInfo;
        }

        worksheet.Cells.AutoFitColumns();
        
        // Convertir a CSV
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", headers.Select(h => EscapeCsv(h))));
        
        foreach (var p in payments)
        {
            csv.AppendLine(string.Join(",", new[]
            {
                EscapeCsv(p.Id.ToString()),
                EscapeCsv(p.PatientName),
                p.Amount.ToString("F2"),
                EscapeCsv(GetMethodDisplayName(p.Method)),
                EscapeCsv(p.Concept),
                EscapeCsv(p.PaidAt.ToString("yyyy-MM-dd HH:mm")),
                EscapeCsv(p.AppointmentInfo ?? "")
            }));
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<byte[]> ExportPaymentsExcelAsync(Guid clinicId, DateTime? startDate = null, DateTime? endDate = null, Guid? patientId = null)
    {
        var payments = await GetPaymentsQuery(clinicId, startDate, endDate, patientId)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Pagos");

        // Headers
        var headers = new[] { "ID", "Paciente", "Monto", "Método", "Concepto", "Fecha Pago", "Cita" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 82, 255));
            cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
        }

        // Data
        for (int i = 0; i < payments.Count; i++)
        {
            var p = payments[i];
            var row = i + 2;
            worksheet.Cells[row, 1].Value = p.Id.ToString();
            worksheet.Cells[row, 2].Value = p.PatientName;
            worksheet.Cells[row, 3].Value = p.Amount;
            worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 4].Value = GetMethodDisplayName(p.Method);
            worksheet.Cells[row, 5].Value = p.Concept;
            worksheet.Cells[row, 6].Value = p.PaidAt;
            worksheet.Cells[row, 6].Style.Numberformat.Format = "yyyy-mm-dd hh:mm";
            worksheet.Cells[row, 7].Value = p.AppointmentInfo;

            // Alternar colores de fila
            if (i % 2 == 0)
            {
                for (int c = 1; c <= headers.Length; c++)
                {
                    worksheet.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[row, c].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(238, 240, 243));
                }
            }
        }

        worksheet.Cells.AutoFitColumns();

        // Totales
        var totalRow = payments.Count + 3;
        worksheet.Cells[totalRow, 2].Value = "TOTAL";
        worksheet.Cells[totalRow, 2].Style.Font.Bold = true;
        worksheet.Cells[totalRow, 3].Value = payments.Sum(p => p.Amount);
        worksheet.Cells[totalRow, 3].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[totalRow, 3].Style.Font.Bold = true;

        return package.GetAsByteArray();
    }

    public async Task<byte[]> ExportPaymentsPdfAsync(Guid clinicId, DateTime? startDate = null, DateTime? endDate = null, Guid? patientId = null)
    {
        var payments = await GetPaymentsQuery(clinicId, startDate, endDate, patientId)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

        // Pre-fetch patient name if patientId is specified
        string? patientName = null;
        if (patientId.HasValue)
        {
            var pat = await _context.Patients.FindAsync(patientId.Value);
            patientName = pat?.FullName;
        }

        var clinic = await _context.Clinics.FindAsync(clinicId);
        var clinicName = clinic?.Name ?? "Clínica";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica"));
                
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("REPORTE DE PAGOS").FontSize(18).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                        col.Item().Text(clinicName).FontSize(12);
                        col.Item().Text($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(10).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                        if (startDate.HasValue || endDate.HasValue)
                        {
                            col.Item().Text($"Período: {startDate?.ToString("yyyy-MM-dd") ?? "Inicio"} - {endDate?.ToString("yyyy-MM-dd") ?? "Fin"}").FontSize(10);
                        }
                        if (patientName != null)
                        {
                            col.Item().Text($"Paciente: {patientName}").FontSize(10);
                        }
                    });
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.5f); // ID
                        columns.RelativeColumn(2f);   // Paciente
                        columns.RelativeColumn(1f);   // Monto
                        columns.RelativeColumn(1.2f); // Método
                        columns.RelativeColumn(2f);   // Concepto
                        columns.RelativeColumn(1.5f); // Fecha
                        columns.RelativeColumn(1.5f); // Cita
                    });

                    // Header
                    table.Header(header =>
                    {
                        var headers = new[] { "ID", "Paciente", "Monto", "Método", "Concepto", "Fecha Pago", "Cita" };
                        foreach (var h in headers)
                        {
                            header.Cell().Background(QuestPDF.Helpers.Colors.Blue.Medium).Padding(5).Text(h).FontColor(QuestPDF.Helpers.Colors.White).Bold();
                        }
                    });

                    // Rows
                    foreach (var p in payments)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4).Text(p.Id.ToString()[..8]).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4).Text(p.PatientName).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{p.Amount:N2}").FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4).Text(GetMethodDisplayName(p.Method)).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4).Text(p.Concept).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4).Text(p.PaidAt.ToString("yyyy-MM-dd HH:mm")).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4).Text(p.AppointmentInfo ?? "-").FontSize(8);
                    }

                    // Total row
                    table.Cell().ColumnSpan(2).Padding(5).Text("TOTAL").Bold().FontSize(10);
                    table.Cell().Padding(5).AlignRight().Text($"{payments.Sum(p => p.Amount):N2}").Bold().FontSize(10);
                    table.Cell().ColumnSpan(4).Padding(5).Text("");
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Página ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" de ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> ExportPatientBalanceCsvAsync(Guid clinicId)
    {
        var balances = await _context.Patients
            .Where(p => p.ClinicId == clinicId)
            .Select(p => new
            {
                p.Id,
                p.FullName,
                p.TotalDue,
                TotalPaid = p.Payments.Sum(pay => pay.Amount),
                Balance = p.TotalDue - p.Payments.Sum(pay => pay.Amount)
            })
            .OrderBy(p => p.FullName)
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Paciente,Total Adeudado,Total Pagado,Saldo");
        
        foreach (var b in balances)
        {
            csv.AppendLine(string.Join(",", new[]
            {
                EscapeCsv(b.FullName),
                b.TotalDue.ToString("F2"),
                b.TotalPaid.ToString("F2"),
                b.Balance.ToString("F2")
            }));
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<byte[]> ExportPatientBalanceExcelAsync(Guid clinicId)
    {
        var balances = await _context.Patients
            .Where(p => p.ClinicId == clinicId)
            .Select(p => new
            {
                p.Id,
                p.FullName,
                p.TotalDue,
                TotalPaid = p.Payments.Sum(pay => pay.Amount),
                Balance = p.TotalDue - p.Payments.Sum(pay => pay.Amount)
            })
            .OrderBy(p => p.FullName)
            .ToListAsync();

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Saldos Pacientes");

        var headers = new[] { "Paciente", "Total Adeudado", "Total Pagado", "Saldo" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 82, 255));
            cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
        }

        for (int i = 0; i < balances.Count; i++)
        {
            var b = balances[i];
            var row = i + 2;
            worksheet.Cells[row, 1].Value = b.FullName;
            worksheet.Cells[row, 2].Value = b.TotalDue;
            worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 3].Value = b.TotalPaid;
            worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 4].Value = b.Balance;
            worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";

            if (i % 2 == 0)
            {
                for (int c = 1; c <= headers.Length; c++)
                {
                    worksheet.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[row, c].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(238, 240, 243));
                }
            }
        }

        // Totales
        var totalRow = balances.Count + 3;
        worksheet.Cells[totalRow, 1].Value = "TOTALES";
        worksheet.Cells[totalRow, 1].Style.Font.Bold = true;
        worksheet.Cells[totalRow, 2].Value = balances.Sum(b => b.TotalDue);
        worksheet.Cells[totalRow, 2].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[totalRow, 2].Style.Font.Bold = true;
        worksheet.Cells[totalRow, 3].Value = balances.Sum(b => b.TotalPaid);
        worksheet.Cells[totalRow, 3].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[totalRow, 3].Style.Font.Bold = true;
        worksheet.Cells[totalRow, 4].Value = balances.Sum(b => b.Balance);
        worksheet.Cells[totalRow, 4].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[totalRow, 4].Style.Font.Bold = true;

        worksheet.Cells.AutoFitColumns();

        return package.GetAsByteArray();
    }

    public async Task<byte[]> ExportPatientBalancePdfAsync(Guid clinicId)
    {
        var balances = await _context.Patients
            .Where(p => p.ClinicId == clinicId)
            .Select(p => new
            {
                p.Id,
                p.FullName,
                p.TotalDue,
                TotalPaid = p.Payments.Sum(pay => pay.Amount),
                Balance = p.TotalDue - p.Payments.Sum(pay => pay.Amount)
            })
            .OrderBy(p => p.FullName)
            .ToListAsync();

        var clinic = await _context.Clinics.FindAsync(clinicId);
        var clinicName = clinic?.Name ?? "Clínica";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica"));
                
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("REPORTE DE SALDOS DE PACIENTES").FontSize(18).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                        col.Item().Text(clinicName).FontSize(12);
                        col.Item().Text($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(10).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    });
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3f);   // Paciente
                        columns.RelativeColumn(1.5f); // Total Adeudado
                        columns.RelativeColumn(1.5f); // Total Pagado
                        columns.RelativeColumn(1.5f); // Saldo
                    });

                    table.Header(header =>
                    {
                        var headers = new[] { "Paciente", "Total Adeudado", "Total Pagado", "Saldo" };
                        foreach (var h in headers)
                        {
                            header.Cell().Background(QuestPDF.Helpers.Colors.Blue.Medium).Padding(5).Text(h).FontColor(QuestPDF.Helpers.Colors.White).Bold();
                        }
                    });

                    foreach (var b in balances)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4).Text(b.FullName).FontSize(9);
                        table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{b.TotalDue:N2}").FontSize(9);
                        table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{b.TotalPaid:N2}").FontSize(9);
                        var saldoCell = table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{b.Balance:N2}").FontSize(9);
                        if (b.Balance > 0)
                            saldoCell.FontColor(QuestPDF.Helpers.Colors.Red.Medium);
                        else
                            saldoCell.FontColor(QuestPDF.Helpers.Colors.Green.Medium);
                    }

                    table.Cell().ColumnSpan(1).Padding(5).Text("TOTALES").Bold().FontSize(10);
                    table.Cell().Padding(5).AlignRight().Text($"{balances.Sum(b => b.TotalDue):N2}").Bold().FontSize(10);
                    table.Cell().Padding(5).AlignRight().Text($"{balances.Sum(b => b.TotalPaid):N2}").Bold().FontSize(10);
                    var totalSaldoCell = table.Cell().Padding(5).AlignRight().Text($"{balances.Sum(b => b.Balance):N2}").Bold().FontSize(10);
                    if (balances.Sum(b => b.Balance) > 0)
                        totalSaldoCell.FontColor(QuestPDF.Helpers.Colors.Red.Medium);
                    else
                        totalSaldoCell.FontColor(QuestPDF.Helpers.Colors.Green.Medium);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Página ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" de ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        });

        return document.GeneratePdf();
    }

    private IQueryable<PaymentSummaryDto> GetPaymentsQuery(Guid clinicId, DateTime? startDate, DateTime? endDate, Guid? patientId)
    {
        var query = _context.Payments
            .Include(p => p.Patient)
            .Include(p => p.Appointment)
            .Where(p => p.ClinicId == clinicId);

        if (startDate.HasValue)
            query = query.Where(p => p.PaidAt >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(p => p.PaidAt <= endDate.Value);
        if (patientId.HasValue)
            query = query.Where(p => p.PatientId == patientId.Value);

        return query.Select(p => new PaymentSummaryDto(
            p.Id,
            p.PatientId,
            p.Patient.FullName,
            p.Amount,
            p.Method,
            p.Concept,
            p.PaidAt,
            p.Appointment != null ? p.Appointment.StartAt.ToString("yyyy-MM-dd HH:mm") : null
        ));
    }

    private static string GetMethodDisplayName(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.Cash => "Efectivo",
            PaymentMethod.CreditCard => "Tarjeta Crédito",
            PaymentMethod.DebitCard => "Tarjeta Débito",
            PaymentMethod.SPEI => "SPEI",
            PaymentMethod.Other => "Otro",
            _ => method.ToString()
        };
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}