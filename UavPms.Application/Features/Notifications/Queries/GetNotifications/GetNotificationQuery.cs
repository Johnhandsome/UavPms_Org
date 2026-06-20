using MediatR;
using UavPms.Application.Features.Notifications.DTOs;

namespace UavPms.Application.Features.Notifications.Queries.GetNotifications;

public record GetNotificationsQuery(Guid UserId) : IRequest<List<NotificationDto>>;