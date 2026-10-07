namespace Library.Application.Common;

public class Result<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public List<string> Errors { get; init; } = new();

    public static Result<T> Ok(T data, string message = "OK") =>
        new() { Success = true, Data = data, Message = message };

    public static Result<T> Fail(string message, params string[] errors) =>
        new() { Success = false, Message = message, Errors = errors.ToList() };
}

public class Result : Result<object>
{
    public static Result Ok(string message = "OK") =>
        new() { Success = true, Message = message };

    public new static Result Fail(string message, params string[] errors) =>
        new() { Success = false, Message = message, Errors = errors.ToList() };
}

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}