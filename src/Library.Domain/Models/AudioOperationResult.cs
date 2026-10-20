namespace Library.Domain.Models;

/// <summary>
/// نتيجة عملية صوتية — بديل Domain-local لـ Result<T> (الموجود في Application.Common)
/// لتجنّب أي اعتماد عرضي بين الطبقات.
/// </summary>
public sealed class AudioOperationResult<T>
{
    public bool Success { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public int HttpStatusCode { get; init; } = 200;

    public static AudioOperationResult<T> Ok(T value) =>
        new() { Success = true, Value = value };

    public static AudioOperationResult<T> Fail(string error, int httpStatusCode = 400) =>
        new() { Success = false, Error = error, HttpStatusCode = httpStatusCode };

    public static AudioOperationResult<T> NotFound(string error = "Not found") =>
        new() { Success = false, Error = error, HttpStatusCode = 404 };

    public static AudioOperationResult<T> Forbidden(string error = "Forbidden") =>
        new() { Success = false, Error = error, HttpStatusCode = 403 };
}