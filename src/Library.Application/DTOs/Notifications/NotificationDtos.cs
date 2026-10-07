using Library.Domain.Enums;

namespace Library.Application.DTOs.Notifications;

public record NotificationDto(Guid Id, string Title, string Message,
    NotificationType Type, bool IsRead, DateTime CreatedAt, Guid? RelatedBookId);

public record BroadcastNotificationRequest(string Title, string Message, NotificationType Type = NotificationType.General);