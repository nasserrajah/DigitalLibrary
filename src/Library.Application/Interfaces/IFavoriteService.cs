public interface IFavoriteService
{
    Task<PagedResult<FavoriteDto>> GetAsync(Guid userId, PaginationQuery q, CancellationToken ct = default);
    Task<Result<FavoriteDto>> AddAsync(Guid userId, Guid bookId, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid userId, Guid bookId, CancellationToken ct = default);
    Task<Result<bool>> ExistsAsync(Guid userId, Guid bookId, CancellationToken ct = default);
}