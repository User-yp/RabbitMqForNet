# RabbitMqForNet

一个轻量级的 .NET RabbitMQ 事件总线库，支持重试机制、死信队列、优雅关闭和 Redis 审计追踪。

## 特性

- 🚀 基于 RabbitMQ 的发布/订阅事件总线
- 🔄 消息处理重试（带消息头重试计数）
- 💀 死信队列支持
- 🛑 优雅关闭（等待正在处理的消息完成）
- 📋 Redis 消息审计追踪（可选）
- 💉 通过 `IServiceCollection` 扩展方法轻松集成
- 🏷️ 基于 `[EventName]` 特性的声明式事件订阅
- 🔧 支持 JSON 和动态两种事件处理器基类
- 📊 结构化日志（`ILogger`）
- 🔒 SSL/TLS 支持
- 🩺 健康检查接口

## 快速开始

### 1. 安装

```bash
dotnet add package RabbitMQForNet
```

### 2. 配置 appsettings.json

```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "ExchangeName": "event_bus",
    "UserName": "guest",
    "Password": "guest",
    "MaxRetryCount": 3,
    "DeadLetterExchange": "event_bus_dlx",
    "HeartbeatSeconds": 30
  },
  "RedisOptions": {
    "ConnectionString": "localhost:6379",
    "DbNumber": 0
  }
}
```

### 3. 注册服务

```csharp
using EventBusMq;
using EventBusMq.Extensions;

// 配置 RabbitMQ
builder.Services.Configure<RabbitMQOptions>(builder.Configuration.GetSection("RabbitMQ"));

// 添加 EventBus（自动扫描程序集中的 EventHandler）
builder.Services.AddEventBus("my_queue", Assembly.GetExecutingAssembly());

var app = builder.Build();

// 启动消费
app.UseEventBus();
```

### 4. 定义事件处理器

```csharp
using EventBusMq.Attributes;
using EventBusMq.EventHandler;

// JSON 强类型处理器
[EventName("OrderCreated")]
public class OrderCreatedHandler : JsonIntegrationEventHandler<OrderCreatedEvent>
{
    public override Task HandleJson(string eventName, OrderCreatedEvent? eventData, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"订单已创建: {eventData?.OrderId}");
        return Task.CompletedTask;
    }
}

// 动态处理器
[EventName("FileUploaded")]
public class FileUploadedHandler : DynamicIntegrationEventHandler
{
    public override async Task HandleDynamic(string eventName, JsonElement eventData, CancellationToken cancellationToken = default)
    {
        var fileName = eventData.GetProperty("FileName").GetString();
        // 处理文件...
    }
}
```

### 5. 发布事件

```csharp
public class MyController(IEventBus eventBus) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder()
    {
        await eventBus.Publish("OrderCreated", new OrderCreatedEvent { OrderId = 123 });
        return Ok();
    }
}
```

## RabbitMQOptions 配置项

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| HostName | string | localhost | RabbitMQ 服务器地址 |
| Port | int | 5672 | RabbitMQ 端口 |
| ExchangeName | string | event_bus | 交换器名称 |
| UserName | string? | null | 用户名 |
| Password | string? | null | 密码 |
| VirtualHost | string | / | 虚拟主机 |
| SslEnabled | bool | false | 是否启用 SSL |
| MaxRetryCount | int | 3 | 消息最大重试次数 |
| DeadLetterExchange | string? | null | 死信交换器名称 |
| HeartbeatSeconds | int | 30 | 心跳间隔（秒） |
| NetworkRecoveryIntervalSeconds | int | 10 | 断线恢复间隔（秒） |
| ShutdownTimeoutSeconds | int | 30 | 优雅关闭超时（秒） |

## 许可证

MIT License
