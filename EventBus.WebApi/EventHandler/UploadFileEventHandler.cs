using EventBusMq.Attributes;
using EventBusMq.EventHandler;
using System.Text.Json;

namespace EventBus.WebApi.EventHandler;

[EventName("UploadFile")]
public class UploadFileEventHandler : DynamicIntegrationEventHandler
{
    public override async Task HandleDynamic(string eventName, JsonElement eventData, CancellationToken cancellationToken = default)
    {
        var fileName = eventData.GetProperty("FileName").GetString() ?? "uploaded_file";
        var fileBytes = eventData.GetProperty("File").GetBytesFromBase64();

        // 使用配置的路径或默认路径
        var uploadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "RabbitMqUploads");

        if (!Directory.Exists(uploadPath))
            Directory.CreateDirectory(uploadPath);

        var filePath = Path.Combine(uploadPath, fileName);
        await File.WriteAllBytesAsync(filePath, fileBytes, cancellationToken);

        Console.WriteLine($"文件已保存: {filePath}");
    }
}
