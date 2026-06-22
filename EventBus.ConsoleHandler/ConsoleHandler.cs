using EventBusMq.Attributes;
using EventBusMq.EventHandler;

namespace EventBus.ConsoleHandler;

[EventName("MqController")]
public class ConsoleHandler : JsonIntegrationEventHandler<Event>
{
    public override Task HandleJson(string eventName, Event? eventData, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"控制台监听Event: {eventData?.EventMessage}, 消息消费时间: {DateTime.Now}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// 与 WebApi 中定义的事件结构保持一致的消息契约。
/// </summary>
public record Event(string EventMessage);
