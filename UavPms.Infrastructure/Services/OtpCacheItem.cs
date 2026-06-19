using System;

namespace UavPms.Infrastructure.Services;

public class OtpCacheItem
{
    public string Code { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
}
