using MediatR;

namespace UavPms.Application.Features.Notifications.Commands.Delete;

public record DeleteNotificationCommand (Guid Id) : IRequest;