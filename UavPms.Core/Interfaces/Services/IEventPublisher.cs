using System.Threading.Tasks;

namespace UavPms.Core.Interfaces.Services;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event) where T : class;
}
