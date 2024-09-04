using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBusMq.EventHandler;

public abstract class DynamicIntegrationEventHandler : IIntegrationEventHandler
{
    public Task Handle(string eventName, string eventData)
    {
        //https://github.com/dotnet/runtime/issues/53195
        //https://github.com/dotnet/core/issues/6444
        //所以暂时用Dynamic.Json来实现。
        dynamic dynamicEventData = JsonConvert.DeserializeObject<dynamic>(eventData);
        return HandleDynamic(eventName, dynamicEventData);
    }
    public abstract Task HandleDynamic(string eventName, dynamic eventData);
}
