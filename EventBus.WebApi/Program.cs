using EventBusMq;
using EventBusMq.Extensions;
using EventBusRedis;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 绑定 RabbitMQ 配置
builder.Services.Configure<RabbitMQOptions>(builder.Configuration.GetSection("RabbitMQ"));

// 绑定 Redis 配置（可选）
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("RedisOptions"));

// 注册 EventBus，自动扫描当前程序集中的 EventHandler
builder.Services.AddEventBus(Assembly.GetExecutingAssembly().GetName().Name!, Assembly.GetExecutingAssembly());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 立即初始化 EventBus，开始消费消息
app.UseEventBus();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
