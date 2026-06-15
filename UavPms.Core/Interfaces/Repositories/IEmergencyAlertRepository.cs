using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Repositories;

public interface IEmergencyAlertRepository : IGenericRepository<EmergencyAlert>
{
    Task<IReadOnlyList<EmergencyAlert>> GetAlertHistoryAsync(
        string? status,
        string? priority,
        DateTime from,
        DateTime to);
}
