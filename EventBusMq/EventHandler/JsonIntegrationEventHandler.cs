using System.Text.Json;

namespace EventBusMq.EventHandler;

public abstract class JsonIntegrationEventHandler<T> : IIntegrationEventHandler
{
    public Task Handle(string eventName, string json, CancellationToken cancellationToken = default)
    {
        T? eventData = JsonSerializer.Deserialize<T>(json);
        return HandleJson(eventName, eventData, cancellationToken);
    }

    public abstract Task HandleJson(string eventName, T? eventData, CancellationToken cancellationToken = default);
}
