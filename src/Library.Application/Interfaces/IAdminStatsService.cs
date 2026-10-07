// IAdminStatsService.cs
public interface IAdminStatsService
{
    Task<Result<object>> GetStatsAsync(CancellationToken ct = default);
}