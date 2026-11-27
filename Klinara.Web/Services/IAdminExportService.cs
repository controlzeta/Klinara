using Klinara.Web.Services;

namespace Klinara.Web.Services;

public interface IAdminExportService
{
    Task<byte[]> ExportPaymentsCsvAsync(Guid clinicId, DateTime? startDate = null, DateTime? endDate = null, Guid? patientId = null);
    Task<byte[]> ExportPaymentsExcelAsync(Guid clinicId, DateTime? startDate = null, DateTime? endDate = null, Guid? patientId = null);
    Task<byte[]> ExportPaymentsPdfAsync(Guid clinicId, DateTime? startDate = null, DateTime? endDate = null, Guid? patientId = null);
    Task<byte[]> ExportPatientBalanceCsvAsync(Guid clinicId);
    Task<byte[]> ExportPatientBalanceExcelAsync(Guid clinicId);
    Task<byte[]> ExportPatientBalancePdfAsync(Guid clinicId);
}