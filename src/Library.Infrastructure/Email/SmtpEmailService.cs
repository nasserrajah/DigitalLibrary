using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

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
        // ═══════════ قراءة الإعدادات ═══════════
        var host = _config["Email:SmtpHost"];
        var portRaw = _config["Email:SmtpPort"] ?? "587";
        var username = _config["Email:Username"];
        var password = _config["Email:Password"];
        var fromAddress = _config["Email:FromAddress"] ?? username;
        var fromName = _config["Email:FromName"] ?? "Smart Library";
        var enableSslRaw = _config["Email:EnableSsl"] ?? "true";

        // ═══════════ التحقق ═══════════
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Email:SmtpHost is missing.");

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Email:Username is missing.");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Email:Password is missing.");

        if (!int.TryParse(portRaw, out var port))
            throw new InvalidOperationException($"Email:SmtpPort '{portRaw}' is invalid.");

        if (!bool.TryParse(enableSslRaw, out var enableSsl))
            throw new InvalidOperationException($"Email:EnableSsl '{enableSslRaw}' is invalid.");

        _log.LogInformation(
            "SMTP → {Host}:{Port}, User: {User}, SSL: {Ssl}",
            host, port, MaskUsername(username), enableSsl);

        // ═══════════ بناء الرسالة ═══════════
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = body };
        message.Body = bodyBuilder.ToMessageBody();

        // ═══════════ تحديد SecureSocketOptions ═══════════
        // MailKit يتعامل مع كل الحالات تلقائياً:
        // - Port 465 → SslOnConnect
        // - Port 587 → StartTls (أو StartTlsWhenAvailable)
        // - Port 25  → StartTlsWhenAvailable
        var socketOptions = port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.StartTlsWhenAvailable,
        };

        // ═══════════ الإرسال ═══════════
        using var client = new SmtpClient();
        client.Timeout = 30000;

        try
        {
            _log.LogInformation("📧 Connecting to {Host}:{Port} ({Mode})...",
                host, port, socketOptions);

            await client.ConnectAsync(host, port, socketOptions, ct);
            _log.LogInformation("✅ Connected. Authenticating as {User}...",
                MaskUsername(username));

            await client.AuthenticateAsync(username, password, ct);
            _log.LogInformation("✅ Authenticated. Sending email to {To}...", to);

            await client.SendAsync(message, ct);
            _log.LogInformation("✅ Email sent successfully to {To}", to);

            await client.DisconnectAsync(true, ct);
        }
        catch (AuthenticationException ex)
        {
            _log.LogError(ex, "❌ Authentication failed for {User}", MaskUsername(username));
            throw new InvalidOperationException(
                "Email authentication failed. Check username/password (use Gmail App Password, not account password).",
                ex);
        }
        catch (SslHandshakeException ex)
        {
            _log.LogError(ex, "❌ SSL handshake failed for {Host}:{Port}", host, port);
            throw new InvalidOperationException(
                $"SSL handshake failed. Try a different port (465 vs 587). Detail: {ex.Message}",
                ex);
        }
        catch (SmtpCommandException ex)
        {
            _log.LogError(ex,
                "❌ SMTP command failed. StatusCode: {Code}, ErrorCode: {Error}",
                ex.StatusCode, ex.ErrorCode);
            throw new InvalidOperationException(
                $"SMTP command failed ({ex.StatusCode}): {ex.Message}", ex);
        }
        catch (SmtpProtocolException ex)
        {
            _log.LogError(ex, "❌ SMTP protocol error");
            throw new InvalidOperationException(
                $"SMTP protocol error: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex)
        {
            _log.LogError(ex, "⏱️ Operation cancelled/timed out");
            throw new InvalidOperationException(
                "Email sending timed out. Server may be unreachable.", ex);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "💥 Unexpected error sending email to {To}", to);
            throw new InvalidOperationException(
                $"Email failed: {ex.GetType().Name} — {ex.Message}", ex);
        }
    }

    private static string MaskUsername(string username)
    {
        if (string.IsNullOrEmpty(username)) return "(empty)";
        if (username.Length <= 4) return "****";

        var atIndex = username.IndexOf('@');
        if (atIndex > 2)
        {
            var local = username.Substring(0, atIndex);
            var domain = username.Substring(atIndex);
            var maskedLocal = local.Substring(0, 2) + new string('*', local.Length - 2);
            return $"{maskedLocal}{domain}";
        }
        return username.Substring(0, 2) + new string('*', username.Length - 2);
    }
}