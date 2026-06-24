namespace UavPms.Core.Interfaces.Repositories;

using UavPms.Core.Entities;

public interface INotificationRepository : IGenericRepository<Notification> {
    public Task<List<Notification>> GetByUserAsync(Guid userId);
    public Task MarkAsReadAsync(Guid id);
    public Task<List<Notification>> GetUnpushedNotificationsWithActiveUserAsync();
}