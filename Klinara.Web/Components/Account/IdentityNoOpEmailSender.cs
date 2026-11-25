using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Klinara.Web.Data;
using Klinara.Web.Services;

namespace Klinara.Web.Components.Account;

// Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
internal sealed class IdentityNoOpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IEmailSender emailSender = new NoOpEmailSender();

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        emailSender.SendEmailAsync(email, "Confirma tu cuenta en Klinara", KlinaraEmailTemplates.AccountConfirmation(user.UserName ?? email, confirmationLink));

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        emailSender.SendEmailAsync(email, "Restablece tu contraseña en Klinara", KlinaraEmailTemplates.PasswordResetLink(user.UserName ?? email, resetLink));

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        emailSender.SendEmailAsync(email, "Tu código de restablecimiento - Klinara", KlinaraEmailTemplates.OtpCodeEmail(user.UserName ?? email, resetCode, "PasswordReset"));
}
