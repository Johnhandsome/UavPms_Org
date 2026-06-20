using System.Threading.Tasks;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Infrastructure.Messaging;

public class NoOpEventPublisher : IEventPublisher
{
    public Task PublishAsync<T>(T @event) where T : class
    {
        return Task.CompletedTask;
    }
}
