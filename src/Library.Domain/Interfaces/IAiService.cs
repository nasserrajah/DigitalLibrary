using Library.Domain.Dtos;

namespace Library.Domain.Interfaces;

public interface IAiService
{
    Task<string> GetBookAdviceAsync(
        string question,
        IEnumerable<AiBookCandidate> books,
        CancellationToken cancellationToken = default);
}