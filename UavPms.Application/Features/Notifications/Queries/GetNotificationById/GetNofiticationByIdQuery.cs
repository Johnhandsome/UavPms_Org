using MediatR;
using UavPms.Application.Features.Notifications.DTOs;

namespace UavPms.Application.Features.Notifications.Queries.GetNotificationById;

public record GetNofiticationByIdQuery(Guid Id) : IRequest<NotificationDto>;
