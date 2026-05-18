using UavPms.Core.Common;

namespace UavPms.Core.Entities;

public class AppUser : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    // Mặc định tạo tài khoản thì được phép hoạt động 
    public bool IsActive { get; set; } = true;
    // Navigation Property: 1 User có thể có nhiều Role (1-n) thông qua bảng trung gian
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}