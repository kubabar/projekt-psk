using Backend.Models;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Backend.Services;

public interface INbpApiQueueService
{
    string EnqueueTask(string currencyCode, string userId);
}

public class NbpApiQueueService : INbpApiQueueService
{
    private readonly IRabbitMqService _rabbitMqService;
    private readonly IConfiguration _configuration;
    private readonly string _queueName;

    public NbpApiQueueService(IRabbitMqService rabbitMqService, IConfiguration configuration)
    {
        _rabbitMqService = rabbitMqService;
        _configuration = configuration;
        _queueName = _configuration["RabbitMQ:Queues:NbpApi"] ?? "nbp.api.queue";
    }

    public string EnqueueTask(string currencyCode, string userId)
    {
        var taskId = Guid.NewGuid().ToString();
        
        var message = new NbpApiTaskMessage
        {
            TaskId = taskId,
            CurrencyCode = currencyCode,
            UserId = userId,
            RequestedAt = DateTime.UtcNow
        };

        using var channel = _rabbitMqService.CreateChannel();
        
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _queueName,
            basicProperties: properties,
            body: body
        );

        return taskId;
    }
}
