using EventBusMq.Attributes;
using EventBusMq.EventHandler;
using EventBusRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Reflection;

namespace EventBusMq.Extensions;

public static class ServicesCollectionExtensions
{
    /// <summary>
    /// 添加EventBus服务，自动扫描assemblies中的EventHandler。
    /// </summary>
    /// <param name="services"></param>
    /// <param name="queueName">如果多个消费者订阅同一个Queue，这时Queue中的消息会被平均分摊给多个消费者进行处理，
    /// 而不是每个消费者都收到所有的消息并处理。为了确保一个应用监听到所有的领域事件，所以不同前端项目的queueName需要不一样。
    /// 因此，对于同一个应用，这个queueName需要保证在多个集群实例和多次运行保持一致，这样可以保证应用重启后仍然能收到没来得及处理的消息。
    /// 而且这样同一个应用的多个集群实例只有一个能收到一条消息，不会同一条消息被一个应用的多个实例处理。这样消息的处理就被平摊到多个实例中。</param>
    /// <param name="assemblies">要扫描的程序集</param>
    public static IServiceCollection AddEventBus(this IServiceCollection services, string queueName, params Assembly[] assemblies)
    {
        return services.AddEventBus(queueName, assemblies.ToList());
    }

    /// <summary>
    /// 添加EventBus服务，自动扫描assemblies中的EventHandler。
    /// </summary>
    public static IServiceCollection AddEventBus(this IServiceCollection services, string queueName, IEnumerable<Assembly> assemblies)
    {
        List<Type> eventHandlers = new();
        foreach (var asm in assemblies)
        {
            // 用GetTypes()，这样非public类也能注册
            var types = asm.GetTypes().Where(t => t.IsAbstract == false
                && t.IsAssignableTo(typeof(IIntegrationEventHandler)));
            eventHandlers.AddRange(types);
        }
        return services.AddEventBus(queueName, eventHandlers);
    }

    /// <summary>
    /// 添加EventBus服务，使用指定的EventHandler类型列表。
    /// </summary>
    /// <param name="services"></param>
    /// <param name="queueName">队列名称</param>
    /// <param name="eventHandlerTypes">实现了IIntegrationEventHandler的类型</param>
    public static IServiceCollection AddEventBus(this IServiceCollection services, string queueName, IEnumerable<Type> eventHandlerTypes)
    {
        // 注册EventHandler为Scoped
        foreach (Type type in eventHandlerTypes)
        {
            services.TryAddScoped(type, type);
        }

        // Redis 是可选的：如果已注册IRedisService则跳过，否则尝试注册默认实现
        if (services.All(sd => sd.ServiceType != typeof(IRedisService)))
        {
            services.TryAddSingleton<IRedisService, RedisService>();
        }

        // 将RabbitMQOptions绑定到配置
        services.TryAddSingleton(sp =>
        {
            var options = sp.GetService<IOptions<RabbitMQOptions>>()?.Value ?? new RabbitMQOptions();
            return options;
        });

        // 注册IEventBus为Singleton
        services.TryAddSingleton<IEventBus>(sp =>
        {
            var options = sp.GetRequiredService<RabbitMQOptions>();
            var logger = sp.GetRequiredService<ILogger<RabbitMQEventBus>>();

            var factory = new ConnectionFactory()
            {
                HostName = options.HostName,
                Port = options.Port,
                DispatchConsumersAsync = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(options.HeartbeatSeconds),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(options.NetworkRecoveryIntervalSeconds),
            };

            if (!string.IsNullOrEmpty(options.UserName))
                factory.UserName = options.UserName;

            if (!string.IsNullOrEmpty(options.Password))
                factory.Password = options.Password;

            if (!string.IsNullOrEmpty(options.VirtualHost))
                factory.VirtualHost = options.VirtualHost;

            if (options.SslEnabled)
            {
                factory.Ssl = new SslOption
                {
                    Enabled = true,
                    ServerName = options.HostName
                };
            }

            // eventBus归DI管理，释放的时候会调用Dispose
            var mqLogger = sp.GetRequiredService<ILogger<RabbitMQConnection>>();
            var mqConnection = new RabbitMQConnection(factory, mqLogger);
            var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var eventBus = new RabbitMQEventBus(mqConnection, serviceScopeFactory, sp, logger, options, options.ExchangeName, queueName);

            // 遍历所有实现了IIntegrationEventHandler接口的类，批量注册到eventBus
            foreach (Type type in eventHandlerTypes)
            {
                // 获取类上标注的EventNameAttribute，EventNameAttribute的Name为要监听的事件的名字
                // 允许监听多个事件，但是不能为空
                var eventNameAttrs = type.GetCustomAttributes<EventNameAttribute>();
                if (eventNameAttrs.Any() == false)
                {
                    throw new ApplicationException($"There should be at least one EventNameAttribute on {type}");
                }
                foreach (var eventNameAttr in eventNameAttrs)
                {
                    eventBus.Subscribe(eventNameAttr.Name, type);
                }
            }

            return eventBus;
        });

        return services;
    }
}
