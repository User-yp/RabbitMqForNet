namespace EventBusRedis;

public class RedisOptions
{
    public string? ConnectionString { get; set; }
    public int DbNumber { get; set; }

    public RedisOptions()
    {
    }

    public RedisOptions(string connectionString, int dbNumber)
    {
        ConnectionString = connectionString;
        DbNumber = dbNumber;
    }
}
