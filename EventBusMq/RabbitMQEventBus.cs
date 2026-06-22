using EventBusMq.EventHandler;
using EventBusRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace EventBusMq;

public class RabbitMQEventBus : IEventBus, IDisposable
{
    private readonly ILogger<RabbitMQEventBus> _logger;
    private IModel? _consumerChannel;
    private readonly string _exchangeName;
    private string _queueName;
    private readonly RabbitMQConnection _persistentConnection;
    private readonly SubscriptionsManager _subsManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRedisService? _redisService;
    private readonly RabbitMQOptions _options;

    private int _isConsuming;
    private int _inFlightCount;
    private readonly ManualResetEventSlim _shutdownEvent = new(true);
    private bool _disposed;

    // 消息头键名
    private const string RetryCountHeader = "x-retry-count";

    public RabbitMQEventBus(
        RabbitMQConnection persistentConnection,
        IServiceScopeFactory scopeFactory,
        IServiceProvider rootServiceProvider,
        ILogger<RabbitMQEventBus> logger,
        RabbitMQOptions options,
        string? exchangeName = null,
        string? queueName = null)
    {
        _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _subsManager = new SubscriptionsManager();
        _exchangeName = exchangeName ?? options.ExchangeName;
        _queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));

        // Redis 是可选的，如果未注册则跳过
        _redisService = rootServiceProvider.GetService<IRedisService>();

        _consumerChannel = CreateConsumerChannel();
        _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;

        _logger.LogInformation("RabbitMQEventBus initialized: exchange={ExchangeName}, queue={QueueName}", _exchangeName, _queueName);
    }

    /// <summary>
    /// 检查事件总线健康状态。
    /// </summary>
    public bool IsHealthy => _persistentConnection.IsConnected && _consumerChannel?.IsOpen == true;

    /// <summary>
    /// 订阅移除的事件处理程序。
    /// </summary>
    private void SubsManager_OnEventRemoved(object? sender, string eventName)
    {
        if (!_persistentConnection.IsConnected)
            _persistentConnection.TryConnect();

        if (_consumerChannel?.IsOpen == true)
        {
            _consumerChannel.QueueUnbind(queue: _queueName, exchange: _exchangeName, routingKey: eventName);
        }

        if (_subsManager.IsEmpty)
        {
            _queueName = string.Empty;
            _consumerChannel?.Close();
            _logger.LogInformation("All subscriptions removed, consumer channel closed");
        }
    }

    /// <summary>
    /// 释放RabbitMQEventBus使用的资源，等待正在处理的消息完成。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation("Shutting down RabbitMQEventBus, waiting for {InFlightCount} in-flight messages to complete", _inFlightCount);

        // 等待正在处理的消息完成（带超时）
        if (!_shutdownEvent.Wait(TimeSpan.FromSeconds(_options.ShutdownTimeoutSeconds)))
        {
            _logger.LogWarning("Shutdown timeout reached with {InFlightCount} messages still in-flight", _inFlightCount);
        }

        _subsManager.OnEventRemoved -= SubsManager_OnEventRemoved;
        _subsManager.Clear();

        _consumerChannel?.Dispose();
        _persistentConnection.Dispose();
        _shutdownEvent.Dispose();

        _logger.LogInformation("RabbitMQEventBus disposed");
    }

    /// <summary>
    /// 将事件发布到消息代理。
    /// </summary>
    public async Task Publish(string eventName, object? eventData)
    {
        if (!_persistentConnection.IsConnected)
            _persistentConnection.TryConnect();

        using var channel = _persistentConnection.CreateModel();
        channel.ExchangeDeclare(exchange: _exchangeName, type: "direct");

        byte[] body;
        if (eventData == null)
            body = Array.Empty<byte>();
        else
            body = JsonSerializer.SerializeToUtf8Bytes(eventData, eventData.GetType(), new JsonSerializerOptions { WriteIndented = false });

        var properties = channel.CreateBasicProperties();
        properties.DeliveryMode = 2; // persistent
        properties.CorrelationId = Guid.NewGuid().ToString();

        // 可选：将事件记录到Redis用于审计追踪
        if (_redisService != null)
        {
            try
            {
                await _redisService.EnqueueAsync(eventName, eventData);
            }
            catch (Exception ex)
            {
                // Redis失败不应阻塞消息发布
                _logger.LogWarning(ex, "Failed to enqueue event {EventName} to Redis (non-blocking)", eventName);
            }
        }

        channel.BasicPublish(_exchangeName, routingKey: eventName, mandatory: true, basicProperties: properties, body: body);
        _logger.LogDebug("Published event {EventName}, correlationId={CorrelationId}", eventName, properties.CorrelationId);
    }

    /// <summary>
    /// 使用指定的处理程序类型订阅事件。
    /// </summary>
    public void Subscribe(string eventName, Type handlerType)
    {
        CheckHandlerType(handlerType);
        DoInternalSubscription(eventName);
        _subsManager.AddSubscription(eventName, handlerType);
        StartBasicConsume();
    }

    /// <summary>
    /// 使用指定的处理程序类型取消订阅事件。
    /// </summary>
    public void Unsubscribe(string eventName, Type handlerType)
    {
        CheckHandlerType(handlerType);
        _subsManager.RemoveSubscription(eventName, handlerType);
    }

    /// <summary>
    /// 检查提供的处理程序类型是否有效。
    /// </summary>
    private static void CheckHandlerType(Type handlerType)
    {
        if (!typeof(IIntegrationEventHandler).IsAssignableFrom(handlerType))
            throw new ArgumentException($"{handlerType} doesn't inherit from IIntegrationEventHandler", nameof(handlerType));
    }

    /// <summary>
    /// 处理订阅逻辑的内部方法。
    /// </summary>
    private void DoInternalSubscription(string eventName)
    {
        if (!_subsManager.HasSubscriptionsForEvent(eventName))
        {
            if (!_persistentConnection.IsConnected)
                _persistentConnection.TryConnect();
            _consumerChannel?.QueueBind(queue: _queueName, exchange: _exchangeName, routingKey: eventName);
        }
    }

    /// <summary>
    /// 开始用于接收消息的基本消费操作。确保只注册一个消费者。
    /// </summary>
    private void StartBasicConsume()
    {
        if (Interlocked.CompareExchange(ref _isConsuming, 1, 0) == 0)
        {
            if (_consumerChannel != null)
            {
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
                consumer.Received += Consumer_Received;
                _consumerChannel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
                _logger.LogInformation("Started consuming on queue {QueueName}", _queueName);
            }
        }
    }

    /// <summary>
    /// 处理接收到的消息的事件处理程序。支持重试计数和死信机制。
    /// </summary>
    private async Task Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
    {
        var eventName = eventArgs.RoutingKey;
        var message = Encoding.UTF8.GetString(eventArgs.Body.Span);
        var correlationId = eventArgs.BasicProperties?.CorrelationId ?? "unknown";
        var retryCount = GetRetryCount(eventArgs.BasicProperties);

        Interlocked.Increment(ref _inFlightCount);
        _shutdownEvent.Reset();

        try
        {
            _logger.LogDebug("Processing event {EventName}, correlationId={CorrelationId}, retry={RetryCount}",
                eventName, correlationId, retryCount);

            await ProcessEvent(eventName, message);
            _consumerChannel?.BasicAck(eventArgs.DeliveryTag, multiple: false);

            _logger.LogDebug("Successfully processed event {EventName}, correlationId={CorrelationId}", eventName, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event {EventName}, correlationId={CorrelationId}, retry={RetryCount}/{MaxRetry}",
                eventName, correlationId, retryCount, _options.MaxRetryCount);

            if (retryCount < _options.MaxRetryCount)
            {
                // 重新发布消息并递增重试计数，然后确认原消息
                try
                {
                    if (_consumerChannel?.IsOpen == true)
                    {
                        var newProps = _consumerChannel.CreateBasicProperties();
                        newProps.DeliveryMode = 2;
                        newProps.CorrelationId = correlationId;
                        newProps.Headers = new Dictionary<string, object>(
                            eventArgs.BasicProperties?.Headers ?? new Dictionary<string, object>())
                        {
                            [RetryCountHeader] = retryCount + 1
                        };

                        _consumerChannel.BasicPublish(_exchangeName, eventName, newProps, eventArgs.Body);
                        _consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);

                        _logger.LogWarning("Requeued event {EventName} for retry {RetryCount}/{MaxRetry}",
                            eventName, retryCount + 1, _options.MaxRetryCount);
                    }
                }
                catch (Exception repubEx)
                {
                    _logger.LogError(repubEx, "Failed to requeue event {EventName}, falling back to Nack with requeue", eventName);
                    try { _consumerChannel?.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: true); } catch { }
                }
            }
            else
            {
                // 超过最大重试次数 — 拒绝消息且不重新入队
                // 如果配置了死信交换器，消息将被路由到死信队列
                try
                {
                    _consumerChannel?.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
                }
                catch (Exception nackEx)
                {
                    _logger.LogError(nackEx, "Failed to Nack event {EventName} after exceeding max retries", eventName);
                }

                _logger.LogError("Event {EventName} exceeded max retries ({MaxRetry}), sent to DLQ or discarded. correlationId={CorrelationId}",
                    eventName, _options.MaxRetryCount, correlationId);
            }
        }
        finally
        {
            if (Interlocked.Decrement(ref _inFlightCount) == 0)
            {
                _shutdownEvent.Set();
            }
        }
    }

    /// <summary>
    /// 从消息属性中获取重试计数。
    /// </summary>
    private static int GetRetryCount(IBasicProperties? properties)
    {
        if (properties?.Headers != null &&
            properties.Headers.TryGetValue(RetryCountHeader, out var value))
        {
            // RabbitMQ headers may store integers as byte[] or long
            if (value is int intValue) return intValue;
            if (value is long longValue) return (int)longValue;
            if (value is byte[] bytes && bytes.Length == 4) return BitConverter.ToInt32(bytes);
        }
        return 0;
    }

    /// <summary>
    /// 创建用于消息消费的消费者通道。
    /// </summary>
    private IModel? CreateConsumerChannel()
    {
        if (!_persistentConnection.IsConnected)
            _persistentConnection.TryConnect();

        if (!_persistentConnection.IsConnected)
        {
            _logger.LogError("Cannot create consumer channel: no RabbitMQ connection available");
            return null;
        }

        var channel = _persistentConnection.CreateModel();
        channel.ExchangeDeclare(exchange: _exchangeName, type: "direct");

        // 配置死信队列（如果指定）
        var arguments = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(_options.DeadLetterExchange))
        {
            arguments["x-dead-letter-exchange"] = _options.DeadLetterExchange;
            arguments["x-dead-letter-routing-key"] = _queueName;
            _logger.LogInformation("Dead letter exchange configured: {DLX}", _options.DeadLetterExchange);
        }

        channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);

        channel.CallbackException += (sender, ea) =>
        {
            _logger.LogError(ea.Exception, "Consumer channel callback exception");
        };

        return channel;
    }

    /// <summary>
    /// 通过调用适当的事件处理程序处理事件。每个处理程序在独立的Scope中执行。
    /// </summary>
    private async Task ProcessEvent(string eventName, string message)
    {
        if (_subsManager.HasSubscriptionsForEvent(eventName))
        {
            var subscriptions = _subsManager.GetHandlersForEvent(eventName);
            foreach (var subscription in subscriptions)
            {
                // 各自在不同的Scope中，避免DbContext等的共享造成如下问题：
                // The instance of entity type cannot be tracked because another instance
                await using var scope = _scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetService(subscription) as IIntegrationEventHandler
                    ?? throw new ApplicationException($"无法创建{subscription}类型的服务");
                await handler.Handle(eventName, message);
            }
        }
        else
        {
            _logger.LogWarning("找不到可以处理eventName={EventName}的处理程序", eventName);
        }
    }
}
