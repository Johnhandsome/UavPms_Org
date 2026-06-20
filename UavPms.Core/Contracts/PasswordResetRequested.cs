using System;

namespace UavPms.Core.Contracts;

public class PasswordResetRequested
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiryTime { get; set; }
}
