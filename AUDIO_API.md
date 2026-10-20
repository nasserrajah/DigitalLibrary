# Audio System — API Reference

All endpoints require JWT. `userId` is derived from JWT claims, never from URL.

## Verified on production (2026-10-20)
- `GET /api/audio/books/{bookId}/segments` → 200 `[]`
- `POST /api/audio/books/{bookId}/segments` → 201 with segment GUID
- `POST /api/audio/segments/{segmentId}/synthesize` → 503 `"TTS provider is not configured."` (expected)
- `PUT /api/audio/books/{bookId}/progress` → 200
- `GET /api/audio/books/{bookId}/progress` → 200 with persisted values

## Endpoints

### GET /api/audio/books/{bookId}/segments
Returns all segments ordered by SegmentIndex.
- 200 → AudioSegmentDto[]
- 403 → book not published
- 404 → book not found

### POST /api/audio/books/{bookId}/segments
Body: `{ segmentIndex, pageNumber, text (≤2000 chars), language }`
- 201 → AudioSegmentDto
- 400 → validation error
- 409 → segmentIndex already used for this book
- 403 → book not published
- 404 → book not found

### POST /api/audio/segments/{segmentId}/synthesize
Idempotent. Returns cached URL if segment already generated and file exists.
- 200 → SynthesizeResponse
- 503 → TTS provider not configured (expected with NullTtsProvider)
- 500 → storage failure

### GET /api/audio/books/{bookId}/progress
Returns progress for the authenticated user only.
- 200 → ListeningProgressDto
- 404 → book not found

### PUT /api/audio/books/{bookId}/progress
Body: `{ lastSegmentIndex, lastPositionSeconds }`
- 200 → ListeningProgressDto
- 400 → validation error
- 403 → book not published
- 404 → book not found

## Storage
Files stored via `IFileStorageService` in `App_Data/storage/audio/`. Only the
storage key is persisted in SQLite. Missing files are re-synthesized on the next
synthesize call.

## TTS Provider
Default: `NullTtsProvider` (returns 503, no feature is disabled).
To enable a real provider: implement `ITextToSpeechProvider` and swap the DI
registration in `Library.Infrastructure/DependencyInjection.cs` (single line).

API keys must be supplied via environment variables (e.g., `Tts__AzureKey`),
never committed to the repository.