namespace EventBusMq;

public class SubscriptionsManager
{
    // key是eventName，值是监听这个事件的实现了IIntegrationEventHandler接口的类型
    private readonly Dictionary<string, List<Type>> _handlers = new();

    public event EventHandler<string>? OnEventRemoved;

    public bool IsEmpty => !_handlers.Keys.Any();
    public void Clear() => _handlers.Clear();

    /// <summary>
    /// 把eventHandlerType类型（实现了IIntegrationEventHandler接口）注册为监听了eventName事件
    /// </summary>
    public void AddSubscription(string eventName, Type eventHandlerType)
    {
        if (!HasSubscriptionsForEvent(eventName))
        {
            _handlers.Add(eventName, new List<Type>());
        }
        // 如果已经注册过，则报错
        if (_handlers[eventName].Contains(eventHandlerType))
        {
            throw new ArgumentException($"Handler Type {eventHandlerType} already registered for '{eventName}'", nameof(eventHandlerType));
        }
        _handlers[eventName].Add(eventHandlerType);
    }

    /// <summary>
    /// 移除对eventName事件的eventHandlerType类型的订阅关系。
    /// </summary>
    public void RemoveSubscription(string eventName, Type handlerType)
    {
        if (!_handlers.ContainsKey(eventName))
        {
            return;
        }

        _handlers[eventName].Remove(handlerType);
        if (!_handlers[eventName].Any())
        {
            _handlers.Remove(eventName);
            OnEventRemoved?.Invoke(this, eventName);
        }
    }

    /// <summary>
    /// 得到名字为eventName的所有监听者。
    /// 如果eventName不存在，返回空集合。
    /// </summary>
    public IEnumerable<Type> GetHandlersForEvent(string eventName) =>
        _handlers.TryGetValue(eventName, out var handlers) ? handlers : Enumerable.Empty<Type>();

    /// <summary>
    /// 是否有类型监听eventName这个事件
    /// </summary>
    public bool HasSubscriptionsForEvent(string eventName) => _handlers.ContainsKey(eventName);
}
