using MediatR;

namespace UavPms.Application.Features.Notifications.Commands.MarkAsRead;

public record MarkNotificationAsReadCommand(Guid Id) : IRequest;
