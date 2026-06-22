using System.Text.Json;

namespace EventBusMq.EventHandler;

public abstract class DynamicIntegrationEventHandler : IIntegrationEventHandler
{
    public Task Handle(string eventName, string eventData, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(eventData);
        var dynamicEventData = doc.RootElement.Clone();
        return HandleDynamic(eventName, dynamicEventData, cancellationToken);
    }

    public abstract Task HandleDynamic(string eventName, JsonElement eventData, CancellationToken cancellationToken = default);
}
