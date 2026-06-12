using System;

namespace UavPms.Core.Contracts;

public class MissionCreatedEvent
{
    public Guid MissionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RouteData { get; set; } = string.Empty;
    public Guid AssignedToUserId { get; set; }
    public string DroneCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
