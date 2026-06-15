using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationController : ControllerBase
{
    private readonly INotificationRepository _notificationRepository;

    public NotificationController(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    /// <summary>
    /// Lấy lịch sử thông báo của một người dùng.
    /// GET: api/notifications/history?userId=xxx
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("UserId is required.");
        }

        if (!Guid.TryParse(userId, out var userGuid))
        {
            return BadRequest("Invalid UserId format.");
        }

        var notifications = await _notificationRepository.GetByUserAsync(userGuid);
        return Ok(notifications);
    }

    /// <summary>
    /// Đánh dấu thông báo là đã đọc.
    /// PUT: api/notifications/{id}/read
    /// </summary>
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _notificationRepository.MarkAsReadAsync(id);
        return Ok(new { Message = "Notification marked as read successfully." });
    }

    /// <summary>
    /// Tạo thông báo mới (dùng cho HTTP/testing).
    /// POST: api/notifications
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateNotification([FromBody] Notification notification)
    {
        if (notification == null)
        {
            return BadRequest("Notification data is null.");
        }

        if (notification.Id == Guid.Empty)
        {
            notification.Id = Guid.NewGuid();
        }
        
        notification.SentAt = DateTime.UtcNow;
        notification.IsRead = false;

        await _notificationRepository.AddAsync(notification);
        return CreatedAtAction(nameof(GetHistory), new { userId = notification.UserId.ToString() }, notification);
    }
}