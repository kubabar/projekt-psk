using Backend.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using NBPExchangeRatesLib;

namespace Backend.Services;

public class NbpApiWorker : BackgroundService
{
    private readonly ILogger<NbpApiWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRabbitMqService _rabbitMqService;
    private readonly IServiceProvider _serviceProvider;
    private IModel? _channel;
    private readonly string _queueName;
    private readonly NbpExchangeRatesApi _nbpApi;

    public NbpApiWorker(
        ILogger<NbpApiWorker> logger, 
        IConfiguration configuration,
        IRabbitMqService rabbitMqService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _rabbitMqService = rabbitMqService;
        _serviceProvider = serviceProvider;
        _queueName = _configuration["RabbitMQ:Queues:NbpApi"] ?? "nbp.api.queue";
        _nbpApi = new NbpExchangeRatesApi();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        _channel = _rabbitMqService.CreateChannel();
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<NbpApiTaskMessage>(json);

                if (message != null)
                {
                    _logger.LogInformation($"Processing NBP API task {message.TaskId} for currency: {message.CurrencyCode}");
                    await ProcessNbpApiTask(message);
                }

                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing NBP API task");
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ProcessNbpApiTask(NbpApiTaskMessage message)
    {
        string? userEmail = null;
        
        try
        {
            // Pobierz email użytkownika
            using var scope = _serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            userEmail = await userService.GetUserEmailAsync(message.UserId);
            
            var rate = _nbpApi.GetLatestExchangeRateDouble(message.CurrencyCode);

            var result = new
            {
                CurrencyCode = message.CurrencyCode,
                Rate = rate,
                Date = DateTime.UtcNow
            };

            var resultJson = JsonSerializer.Serialize(result);

            // Jedna publikacja - wszyscy subskrybenci dostaną
            _rabbitMqService.PublishApiResult(
                message.TaskId,
                message.UserId,
                userEmail ?? "",
                "NBP",
                true,
                resultJson,
                null
            );

            _logger.LogInformation($"NBP API task {message.TaskId} completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in NBP API task {message.TaskId}");
            
            var errorMessage = $"Nie można wyświetlić kursu dla podanego kodu waluty: {ex.Message}";
            
            // Jedna publikacja - wszyscy subskrybenci dostaną
            _rabbitMqService.PublishApiResult(
                message.TaskId,
                message.UserId,
                userEmail ?? "",
                "NBP",
                false,
                null,
                errorMessage
            );
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
