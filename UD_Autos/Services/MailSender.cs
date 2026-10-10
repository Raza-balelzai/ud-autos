using System.Net;
using System.Net.Mail;

namespace UD_Autos.Services;

public interface IMailSender
{
    Task<(bool Ok, string? Error)> SendAsync(string to, string subject, string html, CancellationToken cancellationToken = default);
}

public class MailSender : IMailSender
{
    private readonly ISettingsService _settings;
    private readonly ILogger<MailSender> _logger;

    public MailSender(ISettingsService settings, ILogger<MailSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<(bool Ok, string? Error)> SendAsync(string to, string subject, string html, CancellationToken cancellationToken = default)
    {
        var (ok, mail, error) = await _settings.GetOutboundMailAsync(cancellationToken);
        if (!ok || mail is null)
            return (false, error ?? "Email is not configured. Ask the owner to set it in Settings.");

        try
        {
            using var client = new SmtpClient(mail.Host, mail.Port)
            {
                EnableSsl = mail.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = string.IsNullOrWhiteSpace(mail.UserName)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(mail.UserName, mail.Password)
            };
            using var message = new MailMessage
            {
                From = new MailAddress(mail.FromAddress, mail.FromName),
                Subject = subject,
                Body = html,
                IsBodyHtml = true
            };
            message.To.Add(to);
            await client.SendMailAsync(message, cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed for {Host}", mail.Host);
            return (false, "The reset email could not be sent. Ask the owner to check the mail settings.");
        }
    }
}
