// IUserService.cs
public interface IUserService
{
    Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task<PagedResult<UserDto>> GetPagedAsync(PaginationQuery q, CancellationToken ct = default);
    Task<Result> ToggleActiveAsync(Guid id, CancellationToken ct = default);
}
