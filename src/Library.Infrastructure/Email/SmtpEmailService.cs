using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.Email;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _log;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> log)
    {
        _config = config;
        _log = log;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var host = _config["Email:SmtpHost"];
        var port = int.Parse(_config["Email:SmtpPort"] ?? "587");
        var username = _config["Email:Username"];
        var password = _config["Email:Password"];
        var fromAddress = _config["Email:FromAddress"] ?? username;
        var fromName = _config["Email:FromName"] ?? "Smart Library";
        var enableSsl = bool.Parse(_config["Email:EnableSsl"] ?? "true");

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _log.LogWarning("SMTP not configured. Skipping email to {To}", to);
            return;
        }

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30000,
        };

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress!, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
        };
        message.To.Add(to);

        _log.LogInformation("Sending email to {To}...", to);
        await client.SendMailAsync(message, ct);
        _log.LogInformation("Email sent to {To}", to);
    }
}