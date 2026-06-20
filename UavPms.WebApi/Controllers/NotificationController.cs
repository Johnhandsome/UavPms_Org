using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Asp.Versioning;
using MediatR;
using UavPms.Core.Contracts;
using UavPms.Application.Features.Notifications.Queries.GetNotifications;
using UavPms.Application.Features.Notifications.Queries.GetNotificationById;
using UavPms.Application.Features.Notifications.Commands.Create;
using UavPms.Application.Features.Notifications.Commands.MarkAsRead;
using UavPms.Application.Features.Notifications.Commands.Delete;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/notifications")]
[ApiVersion("1.0")]
public class NotificationController : ControllerBase
{
    private readonly ISender _mediator;

    public NotificationController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new ApiResponse(false, "UserId is required."));
        }

        if (!Guid.TryParse(userId, out var userGuid))
        {
            return BadRequest(new ApiResponse(false, "Invalid UserId format."));
        }

        var query = new GetNotificationsQuery(userGuid);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetNotificationByIdQuery(id);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var command = new MarkNotificationAsReadCommand(id);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Notification marked as read successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest request)
    {
        var command = new CreateNotificationCommand(
            request.UserId, 
            request.Type, 
            request.ReferenceType, 
            request.ReferenceId, 
            request.Title, 
            request.Body
        );
        
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetHistory), new { userId = result.UserId.ToString() }, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var command = new DeleteNotificationCommand(id);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Notification deleted successfully."));
    }

    public record CreateNotificationRequest(
        Guid UserId,
        string Type,
        string ReferenceType,
        Guid? ReferenceId,
        string Title,
        string Body
    );
}
