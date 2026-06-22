using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace EventBusRedis;

public class RedisService : IRedisService
{
    private readonly ILogger<RedisService> _logger;
    private readonly ConnectionMultiplexer _conn;
    private readonly IDatabase _db;

    public RedisService(IOptionsMonitor<RedisOptions> options, ILogger<RedisService> logger)
        : this(options.CurrentValue, logger)
    {
    }

    public RedisService(RedisOptions options, ILogger<RedisService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException("Redis connection string is required", nameof(options));

        var connectionString = options.ConnectionString;
        _conn = ConnectionMultiplexer.Connect(connectionString);
        _db = _conn.GetDatabase(options.DbNumber);

        _logger.LogInformation("RedisService connected, db={DbNumber}", options.DbNumber);
    }

    public async Task<long> EnqueueAsync<T>(string key, T value)
    {
        return await _db.ListRightPushAsync(key, value.ToRedisValue());
    }
}

public static class StackExchangeRedisExtension
{
    public static RedisValue ToRedisValue<T>(this T value)
    {
        if (value == null)
            return RedisValue.Null;

        return value switch
        {
            ValueType => value.ToString()!,
            string s => s,
            _ => JsonSerializer.Serialize(value)
        };
    }
}
