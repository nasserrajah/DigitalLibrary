using Library.Domain.Entities;
using Library.Domain.Enums;
using Library.Domain.Interfaces;
using Library.Domain.Models;
using Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.Services;

public class AudioService : IAudioService
{
    private const int MaxTextLength = 2000;
    private const string AudioFolder = "audio";

    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ITextToSpeechProvider _tts;
    private readonly ILogger<AudioService> _log;

    public AudioService(
        AppDbContext db,
        IFileStorageService storage,
        ITextToSpeechProvider tts,
        ILogger<AudioService> log)
    {
        _db = db;
        _storage = storage;
        _tts = tts;
        _log = log;
    }

    public async Task<AudioOperationResult<IReadOnlyList<AudioSegmentDto>>> GetSegmentsAsync(
        Guid bookId, Guid userId, CancellationToken ct = default)
    {
        var book = await _db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId, ct);
        if (book is null)
            return AudioOperationResult<IReadOnlyList<AudioSegmentDto>>.NotFound("Book not found.");
        if (!book.IsPublished)
            return AudioOperationResult<IReadOnlyList<AudioSegmentDto>>.Forbidden("Book is not published.");

        var segments = await _db.AudioSegments
            .AsNoTracking()
            .Where(s => s.BookId == bookId)
            .OrderBy(s => s.SegmentIndex)
            .ToListAsync(ct);

        var dtos = segments.Select(Map).ToList();
        return AudioOperationResult<IReadOnlyList<AudioSegmentDto>>.Ok(dtos);
    }

    public async Task<AudioOperationResult<AudioSegmentDto>> CreateSegmentAsync(
        Guid bookId, Guid userId, CreateAudioSegmentRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            return AudioOperationResult<AudioSegmentDto>.Fail("Text is required.");
        if (req.Text.Length > MaxTextLength)
            return AudioOperationResult<AudioSegmentDto>.Fail($"Text exceeds {MaxTextLength} characters.");
        if (req.SegmentIndex < 0)
            return AudioOperationResult<AudioSegmentDto>.Fail("SegmentIndex must be >= 0.");
        if (req.PageNumber < 0)
            return AudioOperationResult<AudioSegmentDto>.Fail("PageNumber must be >= 0.");

        var book = await _db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId, ct);
        if (book is null)
            return AudioOperationResult<AudioSegmentDto>.NotFound("Book not found.");
        if (!book.IsPublished)
            return AudioOperationResult<AudioSegmentDto>.Forbidden("Book is not published.");

        var duplicate = await _db.AudioSegments
            .AnyAsync(s => s.BookId == bookId && s.SegmentIndex == req.SegmentIndex, ct);
        if (duplicate)
            return AudioOperationResult<AudioSegmentDto>.Fail(
                $"Segment with index {req.SegmentIndex} already exists for this book.", 409);

        var lang = string.IsNullOrWhiteSpace(req.Language) ? book.Language : req.Language;
        var now = DateTime.UtcNow;

        var entity = new AudioSegment
        {
            BookId = bookId,
            PageNumber = req.PageNumber,
            SegmentIndex = req.SegmentIndex,
            Text = req.Text.Trim(),
            Language = lang,
            Status = AudioSegmentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.AudioSegments.Add(entity);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Created AudioSegment {Id} for Book {BookId} (index {Index})",
            entity.Id, bookId, entity.SegmentIndex);

        return AudioOperationResult<AudioSegmentDto>.Ok(Map(entity));
    }

    public async Task<AudioOperationResult<SynthesizeResponse>> SynthesizeSegmentAsync(
        Guid segmentId, Guid userId, CancellationToken ct = default)
    {
        var segment = await _db.AudioSegments
            .Include(s => s.Book)
            .FirstOrDefaultAsync(s => s.Id == segmentId, ct);

        if (segment is null)
            return AudioOperationResult<SynthesizeResponse>.NotFound("Segment not found.");
        if (segment.Book is null || !segment.Book.IsPublished)
            return AudioOperationResult<SynthesizeResponse>.Forbidden("Book is not published.");

        // Idempotent: already generated + file exists → return current state
        if (segment.Status == AudioSegmentStatus.Generated &&
            !string.IsNullOrWhiteSpace(segment.AudioFileName) &&
            await _storage.ExistsAsync(segment.AudioFileName, ct))
        {
            return AudioOperationResult<SynthesizeResponse>.Ok(new SynthesizeResponse
            {
                Synthesized = true,
                Provider = _tts.Name,
                AudioUrl = _storage.GetPublicUrl(segment.AudioFileName),
                DurationSeconds = segment.DurationSeconds,
                Message = "Already synthesized."
            });
        }

        var ttsRequest = new TtsRequest(segment.Text, segment.Language, segment.VoiceName);
        var ttsResult = await _tts.SynthesizeAsync(ttsRequest, ct);

        if (!ttsResult.Success || ttsResult.AudioBytes is null || ttsResult.AudioBytes.Length == 0)
        {
            segment.Status = AudioSegmentStatus.Failed;
            segment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // 503: provider not configured — app keeps working, TTS is optional
            return AudioOperationResult<SynthesizeResponse>.Fail(
                ttsResult.ErrorMessage ?? "TTS synthesis failed.",
                503);
        }

        try
        {
            using var ms = new MemoryStream(ttsResult.AudioBytes);
            var ext = GuessExtension(ttsResult.ContentType);
            var upload = await _storage.SaveAsync(
                ms, $"segment_{segment.Id}{ext}", AudioFolder, ct);

            segment.AudioFileName = upload.StorageKey;
            segment.VoiceName = ttsResult.VoiceName;
            segment.Status = AudioSegmentStatus.Generated;
            segment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return AudioOperationResult<SynthesizeResponse>.Ok(new SynthesizeResponse
            {
                Synthesized = true,
                Provider = ttsResult.ProviderName,
                AudioUrl = _storage.GetPublicUrl(upload.StorageKey),
                DurationSeconds = segment.DurationSeconds,
                Message = "Synthesized successfully."
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to persist audio for segment {Id}", segment.Id);
            segment.Status = AudioSegmentStatus.Failed;
            segment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return AudioOperationResult<SynthesizeResponse>.Fail(
                "Failed to persist audio file.", 500);
        }
    }

    public async Task<AudioOperationResult<ListeningProgressDto>> GetProgressAsync(
        Guid bookId, Guid userId, CancellationToken ct = default)
    {
        var exists = await _db.Books.AnyAsync(b => b.Id == bookId, ct);
        if (!exists)
            return AudioOperationResult<ListeningProgressDto>.NotFound("Book not found.");

        var progress = await _db.ListeningProgresses
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.BookId == bookId && p.UserId == userId, ct);

        if (progress is null)
        {
            return AudioOperationResult<ListeningProgressDto>.Ok(new ListeningProgressDto
            {
                BookId = bookId,
                LastSegmentIndex = 0,
                LastPositionSeconds = 0,
                UpdatedAt = DateTime.UtcNow
            });
        }

        return AudioOperationResult<ListeningProgressDto>.Ok(new ListeningProgressDto
        {
            BookId = progress.BookId,
            LastSegmentIndex = progress.LastSegmentIndex,
            LastPositionSeconds = progress.LastPositionSeconds,
            UpdatedAt = progress.UpdatedAt ?? progress.CreatedAt
        });
    }

    public async Task<AudioOperationResult<ListeningProgressDto>> UpdateProgressAsync(
        Guid bookId, Guid userId, UpdateListeningProgressRequest req, CancellationToken ct = default)
    {
        if (req.LastSegmentIndex < 0)
            return AudioOperationResult<ListeningProgressDto>.Fail("LastSegmentIndex must be >= 0.");
        if (req.LastPositionSeconds < 0)
            return AudioOperationResult<ListeningProgressDto>.Fail("LastPositionSeconds must be >= 0.");

        var book = await _db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId, ct);
        if (book is null)
            return AudioOperationResult<ListeningProgressDto>.NotFound("Book not found.");
        if (!book.IsPublished)
            return AudioOperationResult<ListeningProgressDto>.Forbidden("Book is not published.");

        var progress = await _db.ListeningProgresses
            .FirstOrDefaultAsync(p => p.BookId == bookId && p.UserId == userId, ct);

        var now = DateTime.UtcNow;
        if (progress is null)
        {
            progress = new ListeningProgress
            {
                BookId = bookId,
                UserId = userId,
                LastSegmentIndex = req.LastSegmentIndex,
                LastPositionSeconds = req.LastPositionSeconds,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.ListeningProgresses.Add(progress);
        }
        else
        {
            progress.LastSegmentIndex = req.LastSegmentIndex;
            progress.LastPositionSeconds = req.LastPositionSeconds;
            progress.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        return AudioOperationResult<ListeningProgressDto>.Ok(new ListeningProgressDto
        {
            BookId = progress.BookId,
            LastSegmentIndex = progress.LastSegmentIndex,
            LastPositionSeconds = progress.LastPositionSeconds,
            UpdatedAt = progress.UpdatedAt ?? progress.CreatedAt
        });
    }

    private AudioSegmentDto Map(AudioSegment s) => new()
    {
        Id = s.Id,
        BookId = s.BookId,
        PageNumber = s.PageNumber,
        SegmentIndex = s.SegmentIndex,
        Text = s.Text,
        Language = s.Language,
        VoiceName = s.VoiceName,
        DurationSeconds = s.DurationSeconds,
        Status = s.Status.ToString(),
        AudioUrl = string.IsNullOrWhiteSpace(s.AudioFileName)
            ? null
            : _storage.GetPublicUrl(s.AudioFileName),
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };

    private static string GuessExtension(string? contentType) =>
        contentType?.ToLowerInvariant() switch
        {
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/wav" or "audio/x-wav" => ".wav",
            "audio/ogg" => ".ogg",
            _ => ".mp3"
        };
}