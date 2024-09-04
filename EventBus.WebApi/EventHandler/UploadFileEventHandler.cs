using EventBusMq.Attributes;
using EventBusMq.EventHandler;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace EventBus.WebApi.EventHandler;

[EventName("FileService.UploadFile")]
public class UploadFileEventHandler : DynamicIntegrationEventHandler
{
    public override async Task HandleDynamic(string eventName, dynamic eventData)
    {
        CancellationToken can = default;
        var fileName = (string)eventData.FileName;
        var fileBytes = (byte[])eventData.File;

        using var memoryStream = new MemoryStream(fileBytes);

        using (var fileStream = File.Create($"C:\\Users\\25069\\Desktop\\{fileName}"))
        {
            await memoryStream.CopyToAsync(fileStream);
        }
    }
    
}
