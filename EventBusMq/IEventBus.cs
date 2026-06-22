namespace EventBusMq;

public interface IEventBus : IDisposable
{
    /// <summary>
    /// 将事件发布到消息代理。
    /// </summary>
    Task Publish(string eventName, object? eventData);

    void Subscribe(string eventName, Type handlerType);

    void Unsubscribe(string eventName, Type handlerType);

    /// <summary>
    /// 检查事件总线是否健康（连接正常、消费者通道正常）。
    /// </summary>
    bool IsHealthy { get; }
}
