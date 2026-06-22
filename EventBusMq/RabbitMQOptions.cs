namespace EventBusMq;

public class RabbitMQOptions
{
    /// <summary>
    /// RabbitMQ 服务器主机名
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ 服务器端口
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// 交换器名称
    /// </summary>
    public string ExchangeName { get; set; } = "event_bus";

    /// <summary>
    /// 用户名
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 虚拟主机
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// 是否启用 SSL/TLS
    /// </summary>
    public bool SslEnabled { get; set; } = false;

    /// <summary>
    /// 消息处理最大重试次数，超过后消息将被丢弃或进入死信队列
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// 死信交换器名称（可选，不配置则超过最大重试次数的消息将被丢弃）
    /// </summary>
    public string? DeadLetterExchange { get; set; }

    /// <summary>
    /// 心跳间隔（秒），默认30秒
    /// </summary>
    public int HeartbeatSeconds { get; set; } = 30;

    /// <summary>
    /// 断线自动恢复间隔（秒），默认10秒
    /// </summary>
    public int NetworkRecoveryIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// 优雅关闭超时（秒），等待正在处理的消息完成的最大时间
    /// </summary>
    public int ShutdownTimeoutSeconds { get; set; } = 30;
}
