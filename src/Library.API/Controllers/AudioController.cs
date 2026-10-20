using System.Security.Claims;
using Library.Domain.Interfaces;
using Library.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/audio")]
[Authorize]
public class AudioController : ControllerBase
{
    private readonly IAudioService _audio;

    public AudioController(IAudioService audio) => _audio = audio;

    private Guid GetUserIdOrThrow()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(claim, out var id))
            throw new UnauthorizedAccessException("Invalid user identity.");
        return id;
    }

    [HttpGet("books/{bookId:guid}/segments")]
    public async Task<IActionResult> GetSegments(Guid bookId, CancellationToken ct)
    {
        var userId = GetUserIdOrThrow();
        var result = await _audio.GetSegmentsAsync(bookId, userId, ct);
        return Map(result);
    }

    [HttpPost("books/{bookId:guid}/segments")]
    public async Task<IActionResult> CreateSegment(
        Guid bookId, [FromBody] CreateAudioSegmentRequest req, CancellationToken ct)
    {
        var userId = GetUserIdOrThrow();
        var result = await _audio.CreateSegmentAsync(bookId, userId, req, ct);
        return Map(result, created: true);
    }

    [HttpPost("segments/{segmentId:guid}/synthesize")]
    public async Task<IActionResult> SynthesizeSegment(Guid segmentId, CancellationToken ct)
    {
        var userId = GetUserIdOrThrow();
        var result = await _audio.SynthesizeSegmentAsync(segmentId, userId, ct);
        return Map(result);
    }

    [HttpGet("books/{bookId:guid}/progress")]
    public async Task<IActionResult> GetProgress(Guid bookId, CancellationToken ct)
    {
        var userId = GetUserIdOrThrow();
        var result = await _audio.GetProgressAsync(bookId, userId, ct);
        return Map(result);
    }

    [HttpPut("books/{bookId:guid}/progress")]
    public async Task<IActionResult> UpdateProgress(
        Guid bookId, [FromBody] UpdateListeningProgressRequest req, CancellationToken ct)
    {
        var userId = GetUserIdOrThrow();
        var result = await _audio.UpdateProgressAsync(bookId, userId, req, ct);
        return Map(result);
    }

    private IActionResult Map<T>(AudioOperationResult<T> result, bool created = false)
    {
        if (!result.Success)
            return StatusCode(result.HttpStatusCode, new { error = result.Error });

        return created
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Ok(result.Value);
    }
}