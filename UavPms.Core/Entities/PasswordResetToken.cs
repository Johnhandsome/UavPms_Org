using System;
using UavPms.Core.Common;

namespace UavPms.Core.Entities;

public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;
    
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public bool Used { get; set; }
}
