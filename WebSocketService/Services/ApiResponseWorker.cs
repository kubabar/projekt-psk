using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using WebSocketService.Hubs;

namespace WebSocketService.Services;

public class ApiResponseWorker : BackgroundService
{
    private readonly ILogger<ApiResponseWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRabbitMqService _rabbitMqService;
    private readonly IHubContext<NotificationHub> _hubContext;
    private IModel? _channel;

    public ApiResponseWorker(
        ILogger<ApiResponseWorker> logger,
        IConfiguration configuration,
        IRabbitMqService rabbitMqService,
        IHubContext<NotificationHub> hubContext)
    {
        _logger = logger;
        _configuration = configuration;
        _rabbitMqService = rabbitMqService;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ApiResponseWorker starting...");

        await Task.Delay(5000, stoppingToken);

        try
        {
            var connection = _rabbitMqService.CreateConnection();
            _channel = connection.CreateModel();

            var exchangeName = _configuration["RabbitMQ:Exchanges:ApiResults"] ?? "api.results.exchange";
            var queueName = _configuration["RabbitMQ:Queues:ApiResultsWebSocket"] ?? "api.results.websocket.queue";

            // Subskrybuj fanout exchange
            _channel.ExchangeDeclare(exchangeName, ExchangeType.Fanout, durable: true);
            _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queueName, exchangeName, ""); // Routing key ignored in fanout
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);

                    _logger.LogInformation("Received API result: {Json}", json);

                    var message = JsonSerializer.Deserialize<ApiResultMessage>(json);

                    if (message != null)
                    {
                        await ProcessApiResult(message);
                    }

                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing API result");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

            _logger.LogInformation("ApiResponseWorker listening on queue: {Queue}", queueName);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in ApiResponseWorker");
            throw;
        }
    }

    private async Task ProcessApiResult(ApiResultMessage message)
    {
        try
        {
            var connectionId = NotificationHub.GetConnectionId(message.UserId);

            if (connectionId != null)
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ApiResponse", new
                {
                    taskId = message.TaskId,
                    success = message.Success,
                    data = message.Data != null ? JsonSerializer.Deserialize<object>(message.Data) : null,
                    error = message.Error,
                    completedAt = message.CompletedAt
                });

                _logger.LogInformation("API response sent to user {UserId} for task {TaskId}",
                    message.UserId, message.TaskId);
            }
            else
            {
                _logger.LogWarning("User {UserId} not connected, cannot send API response for task {TaskId}",
                    message.UserId, message.TaskId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send API response to user {UserId}", message.UserId);
            throw;
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}

public class ApiResponseMessage
{
    public required string TaskId { get; set; }
    public required string UserId { get; set; }
    public bool Success { get; set; }
    public string? Data { get; set; }
    public string? Error { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class ApiResultMessage
{
    public required string TaskId { get; set; }
    public required string UserId { get; set; }
    public required string UserEmail { get; set; }
    public required string ApiType { get; set; }
    public bool Success { get; set; }
    public string? Data { get; set; }
    public string? Error { get; set; }
    public DateTime CompletedAt { get; set; }
}
