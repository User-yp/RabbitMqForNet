using EventBus.WebApi.EventHandler;
using EventBusMq;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.WebApi.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class RabbitMqController(IEventBus eventBus) : ControllerBase
{
    private readonly IEventBus _eventBus = eventBus;

    [HttpGet]
    public async Task<ActionResult<string>> RabbitMqTestAsync()
    {
        string str = $"eventBus发送消息{DateTime.Now}";
        Console.WriteLine(str);
        await _eventBus.Publish("MqController", new Event(str));
        return Ok("success!");
    }

    [HttpPost]
    public async Task<ActionResult<string>> UploadExcelAsync(IFormFile excelFile)
    {
        using var memoryStream = new MemoryStream();
        await excelFile.CopyToAsync(memoryStream);
        var streamBytes = memoryStream.ToArray();
        await _eventBus.Publish("UploadFile", new { File = streamBytes, FileName = excelFile.FileName });
        return Ok("success!");
    }
}
