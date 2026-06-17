using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Repositories;

public interface IMaintenanceTicketRepository : IGenericRepository<MaintenanceTicket>
{
    Task<MaintenanceTicket> GetByIdWithDetailsAsync(Guid id);
    Task<MaintenanceTicket?> GetByCodeWithDetailsAsync(string ticketCode);
    Task<IReadOnlyList<MaintenanceTicket>> GetAllWithDetailsAsync();
    Task<IReadOnlyList<MaintenanceTicket>> GetByTechnicianIdWithDetailsAsync(Guid technicianId);
}