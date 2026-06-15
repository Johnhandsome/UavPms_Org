using System.Threading.Tasks;
using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Repositories;

public interface IUavRepository : IGenericRepository<Uav>
{
    Task<Uav?> GetByUavCodeAsync(string code);
}
