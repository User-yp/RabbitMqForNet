using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventBusMq;

public class RabbitMQConnection : IDisposable
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMQConnection> _logger;
    private IConnection? _connection;
    private bool _disposed;
    private readonly object _syncRoot = new();
    private DateTime _lastReconnectAttempt = DateTime.MinValue;
    private static readonly TimeSpan ReconnectCooldown = TimeSpan.FromSeconds(5);

    public RabbitMQConnection(IConnectionFactory connectionFactory, ILogger<RabbitMQConnection> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取当前连接状态。
    /// </summary>
    public bool IsConnected
    {
        get
        {
            return _connection != null && _connection.IsOpen && !_disposed;
        }
    }

    /// <summary>
    /// 创建一个新的通道模型。
    /// </summary>
    public IModel CreateModel()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
        }

        return _connection!.CreateModel();
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _connection?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing RabbitMQ connection");
        }
        _connection = null;
    }

    /// <summary>
    /// 尝试连接到RabbitMQ服务器。
    /// </summary>
    public bool TryConnect()
    {
        lock (_syncRoot)
        {
            if (_disposed) return false;

            // 防止频繁重连
            var timeSinceLastAttempt = DateTime.UtcNow - _lastReconnectAttempt;
            if (timeSinceLastAttempt < ReconnectCooldown)
            {
                _logger.LogDebug("Skipping reconnect attempt — cooldown period has not elapsed ({Remaining:F1}s remaining)",
                    (ReconnectCooldown - timeSinceLastAttempt).TotalSeconds);
                return IsConnected;
            }

            _lastReconnectAttempt = DateTime.UtcNow;

            // 释放旧连接
            if (_connection != null)
            {
                try
                {
                    _connection.ConnectionShutdown -= OnConnectionShutdown;
                    _connection.CallbackException -= OnCallbackException;
                    _connection.ConnectionBlocked -= OnConnectionBlocked;
                    _connection.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing old RabbitMQ connection during reconnect");
                }
                _connection = null;
            }

            try
            {
                _connection = _connectionFactory.CreateConnection();

                if (IsConnected)
                {
                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    _connection.CallbackException += OnCallbackException;
                    _connection.ConnectionBlocked += OnConnectionBlocked;
                    _logger.LogInformation("Successfully connected to RabbitMQ");
                    return true;
                }
                else
                {
                    _logger.LogWarning("RabbitMQ connection created but not in open state");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                return false;
            }
        }
    }

    private void OnConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;
        _logger.LogWarning("RabbitMQ connection blocked: {Reason}", e.Reason);
        TryConnect();
    }

    private void OnCallbackException(object? sender, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;
        _logger.LogError(e.Exception, "RabbitMQ callback exception");
        TryConnect();
    }

    private void OnConnectionShutdown(object? sender, ShutdownEventArgs reason)
    {
        if (_disposed) return;
        _logger.LogWarning("RabbitMQ connection shutdown: {ReplyText}", reason.ReplyText);
        TryConnect();
    }
}
