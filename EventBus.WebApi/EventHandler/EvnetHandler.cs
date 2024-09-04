using EventBusMq.Attributes;
using EventBusMq.EventHandler;

namespace EventBus.WebApi.EventHandler;

[EventName("RabbitMqController")]
public class EvnetHandler : JsonIntegrationEventHandler<Event>
{
    public override Task HandleJson(string eventName, Event? eventData)
    {
        Console.WriteLine($"监听Evnet{eventData.EventMessage},消息消费{DateTime.Now}");
        return Task.CompletedTask;
    }
}
