using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace EventBusMq.Extensions;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 立即加载IEventBus，触发扫描所有EventHandler并开始消费消息。
    /// </summary>
    public static IApplicationBuilder UseEventBus(this IApplicationBuilder appBuilder)
    {
        // 获得IEventBus一次，就会立即加载IEventBus，这样扫描所有的EventHandler，保证消息及时消费
        object? eventBus = appBuilder.ApplicationServices.GetService(typeof(IEventBus));
        if (eventBus == null)
        {
            var loggerFactory = appBuilder.ApplicationServices.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            var logger = loggerFactory?.CreateLogger("EventBusMq");
            logger?.LogError("找不到IEventBus实例，请确保已调用AddEventBus()");
            throw new ApplicationException("找不到IEventBus实例，请确保已调用AddEventBus()");
        }

        return appBuilder;
    }
}
