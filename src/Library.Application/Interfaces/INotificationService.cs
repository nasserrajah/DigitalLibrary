// INotificationService.cs
public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetAsync(Guid userId, PaginationQuery q, CancellationToken ct = default);
    Task<Result> MarkAsReadAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<Result> BroadcastAsync(BroadcastNotificationRequest req, CancellationToken ct = default);
    Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
}