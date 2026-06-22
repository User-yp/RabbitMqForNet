using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBusRedis;

public interface IRedisService
{
    Task<long> EnqueueAsync<T>(string key, T value);
}
