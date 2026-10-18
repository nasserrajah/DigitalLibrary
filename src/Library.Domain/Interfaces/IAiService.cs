using Library.Domain.DTOs;

namespace Library.Domain.Interfaces;

public interface IAiService
{
    Task<string> GetBookAdviceAsync(
        string userName,
        string question,
        IEnumerable<AiBookCandidate> availableBooks,
        bool isFirstMessage = true,
        string historyContext = "",
        CancellationToken cancellationToken = default);
}