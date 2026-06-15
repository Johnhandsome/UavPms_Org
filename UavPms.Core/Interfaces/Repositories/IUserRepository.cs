using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    // Tạm thời để trống, sau này có thể thêm hàm GetByEmailAsync
}