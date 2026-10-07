using AutoMapper;
using AutoMapper.QueryableExtensions;
using Library.Application.Common;
using Library.Application.DTOs.Common;
using Library.Application.DTOs.Notifications;
using Library.Application.Interfaces;
using Library.Domain.Entities;
using Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    public NotificationService(AppDbContext db, IMapper mapper) { _db = db; _mapper = mapper; }

    public async Task<PagedResult<NotificationDto>> GetAsync(Guid userId, PaginationQuery q, CancellationToken ct = default)
    {
        var query = _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .ProjectTo<NotificationDto>(_mapper.ConfigurationProvider).ToListAsync(ct);
        return new PagedResult<NotificationDto> { Items = items, TotalCount = total, Page = q.Page, PageSize = q.PageSize };
    }

    public async Task<Result> MarkAsReadAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (n is null) return Result.Fail("Not found");
        n.IsRead = true; n.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Ok("Marked as read");
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (n is null) return Result.Fail("Not found");
        _db.Notifications.Remove(n);
        await _db.SaveChangesAsync(ct);
        return Result.Ok("Deleted");
    }

    public async Task<Result> BroadcastAsync(BroadcastNotificationRequest req, CancellationToken ct = default)
    {
        var userIds = await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct);
        var list = userIds.Select(uid => new Notification
        {
            UserId = uid, Title = req.Title, Message = req.Message, Type = req.Type
        });
        _db.Notifications.AddRange(list);
        await _db.SaveChangesAsync(ct);
        return Result.Ok($"Sent to {userIds.Count} users");
    }

    public async Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => Result<int>.Ok(await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct));
}