using MediatR;
using UavPms.Application.Features.Notifications.DTOs;

namespace UavPms.Application.Features.Notifications.Queries.GetNotificationById;

public record GetNotificationByIdQuery(Guid Id) : IRequest<NotificationDto>;
