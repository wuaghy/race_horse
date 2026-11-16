using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Email;

public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Email delivery is disabled. Set Email__Enabled=true to enable SMTP delivery.");
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.FromAddress))
            throw new InvalidOperationException("Email:Host and Email:FromAddress must be configured before sending email.");

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };
        message.To.Add(new MailAddress(to));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, System.Text.Encoding.UTF8, "text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, System.Text.Encoding.UTF8, "text/html"));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrWhiteSpace(_options.UserName))
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password ?? "");

        await client.SendMailAsync(message, cancellationToken);
    }
}
