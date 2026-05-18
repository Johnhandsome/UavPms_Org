using UavPms.Core.Common;

namespace UavPms.Core.Entities;

public class AppRole : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}