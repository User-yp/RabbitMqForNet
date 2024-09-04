using EventBus.WebApi.EventHandler;
using EventBusMq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EventBus.WebApi.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class RabbitMqController(IEventBus eventBus) : ControllerBase
{
    private readonly IEventBus eventBus = eventBus;

    [HttpGet]
    public async Task<ActionResult<string>> RabbitMqTestAsync()
    {
        string str = $"eventBus发送消息{DateTime.Now}";
        Console.WriteLine(str);
        eventBus.Publish("RabbitMqController", new Event(str));
        return Ok("success!");
    }

    [HttpPost]
    public async Task<ActionResult<string>> UploadExcelAsync(IFormFile excelFile)
    {
        using (var memoryStream = new MemoryStream())
        {
            await excelFile.CopyToAsync(memoryStream);
            var streamBytes = memoryStream.ToArray();
            eventBus.Publish("FileService.UploadFile", new { File = streamBytes, excelFile.FileName });
        }
        return Ok("success!");
    }
}
