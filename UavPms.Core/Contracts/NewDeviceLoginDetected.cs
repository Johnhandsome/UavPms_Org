using System;

namespace UavPms.Core.Contracts;

public class NewDeviceLoginDetected
{
    public string Email { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime LoginAt { get; set; }
}
