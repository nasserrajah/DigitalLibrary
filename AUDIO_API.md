# Audio System — API Reference

All endpoints require JWT authentication (`Authorization: Bearer <token>`).
`userId` is always derived from the JWT `sub`/`nameid` claim — **never** from the URL.

## Endpoints

### GET `/api/audio/books/{bookId}/segments`
Returns all audio segments for a book, ordered by `segmentIndex`.

**200 OK** → `AudioSegmentDto[]`

**404** → book not found
**403** → book not published

---

### POST `/api/audio/books/{bookId}/segments`
Creates a new text segment for a book.

Body:
```json
{
  "segmentIndex": 0,
  "pageNumber": 1,
  "text": "… (max 2000 chars) …",
  "language": "ar"
}