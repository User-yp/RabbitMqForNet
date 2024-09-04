using EventBusMq;
using EventBusMq.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.Reflection;


var builder = WebApplication.CreateBuilder(args);
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//init RabbitMQOptions
builder.Services.Configure<IntegrationEventRabbitMQOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddEventBus("EventBus.WebApi", ReflectionHelper.GetAllReferencedAssemblies());


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseEventBus();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
