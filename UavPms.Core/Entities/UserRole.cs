using UavPms.Core.Common;

namespace UavPms.Core.Entities;

public class UserRole : BaseEntity
{
    // Khóa ngoại trỏ về App User 
    public Guid UserId { get; set; }
    // Khóa ngoại trỏ về App Role   
    public Guid RoleId { get; set; }
    
    // Navigation Property
    public virtual AppUser? User { get; set; }
    public virtual AppRole? Role { get; set; }
}