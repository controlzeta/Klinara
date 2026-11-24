namespace Klinara.Web.Services;

public class PatientPortalSessionState
{
    public string? ClinicSubdomain { get; set; }
    public string? ClinicName { get; set; }
    public string? PatientEmail { get; set; }
    public string? PatientName { get; set; }
    public string? PortalToken { get; set; }
    public string? ManagementToken { get; set; }
    public string? ConfirmationToken { get; set; }
    public Guid? LastAppointmentId { get; set; }
    public PortalAppointmentView? CurrentAppointmentView { get; set; }

    public bool HasValidPortalToken => !string.IsNullOrWhiteSpace(PortalToken);

    public void Clear()
    {
        PortalToken = null;
        ManagementToken = null;
        ConfirmationToken = null;
        LastAppointmentId = null;
        CurrentAppointmentView = null;
    }
}
