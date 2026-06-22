using EventBusMq.Attributes;
using EventBusMq.EventHandler;

namespace EventBus.WebApi.EventHandler;

[EventName("MqController")]
public class WebApiEventHandler : JsonIntegrationEventHandler<Event>
{
    public override Task HandleJson(string eventName, Event? eventData, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"WebApi监听Event: {eventData?.EventMessage}, 消息消费时间: {DateTime.Now}");
        return Task.CompletedTask;
    }
}
