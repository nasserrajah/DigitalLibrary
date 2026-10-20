using Library.Domain.Models;

namespace Library.Domain.Interfaces;

public interface IAudioService
{
    Task<AudioOperationResult<IReadOnlyList<AudioSegmentDto>>> GetSegmentsAsync(
        Guid bookId, Guid userId, CancellationToken ct = default);

    Task<AudioOperationResult<AudioSegmentDto>> CreateSegmentAsync(
        Guid bookId, Guid userId, CreateAudioSegmentRequest req, CancellationToken ct = default);

    Task<AudioOperationResult<SynthesizeResponse>> SynthesizeSegmentAsync(
        Guid segmentId, Guid userId, CancellationToken ct = default);

    Task<AudioOperationResult<ListeningProgressDto>> GetProgressAsync(
        Guid bookId, Guid userId, CancellationToken ct = default);

    Task<AudioOperationResult<ListeningProgressDto>> UpdateProgressAsync(
        Guid bookId, Guid userId, UpdateListeningProgressRequest req, CancellationToken ct = default);
}