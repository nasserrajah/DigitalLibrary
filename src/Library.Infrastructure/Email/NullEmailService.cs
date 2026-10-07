using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.Email;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

// Development-only: logs the email body instead of sending.
public class NullEmailService : IEmailService
{
    private readonly ILogger<NullEmailService> _log;
    public NullEmailService(ILogger<NullEmailService> log) => _log = log;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _log.LogInformation("📧 [DEV EMAIL] To={To} Subject={Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}