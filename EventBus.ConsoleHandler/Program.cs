using EventBusMq;
using EventBusMq.Extensions;
using EventBusRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

try
{
    var builder = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            // 绑定 RabbitMQ 配置
            services.Configure<RabbitMQOptions>(context.Configuration.GetSection("RabbitMQ"));

            // 绑定 Redis 配置（可选）
            services.Configure<RedisOptions>(context.Configuration.GetSection("RedisOptions"));

            // 注册 EventBus，自动扫描当前程序集中的 EventHandler
            services.AddEventBus(Assembly.GetExecutingAssembly().GetName().Name!, Assembly.GetExecutingAssembly());
        });

    var app = builder.Build();

    // 注意：使用 Host 时，需要手动触发 EventBus 初始化
    var eventBus = app.Services.GetRequiredService<IEventBus>();
    Console.WriteLine("ConsoleHandler 已启动，正在监听消息...");

    await app.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"启动失败: {ex}");
}
