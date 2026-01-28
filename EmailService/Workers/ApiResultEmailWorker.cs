using EmailService.Models;
using EmailService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace EmailService.Workers;

public class ApiResultEmailWorker : BackgroundService
{
    private readonly ILogger<ApiResultEmailWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRabbitMqConnectionService _rabbitMqService;
    private readonly IEmailSender _emailSender;
    private IModel? _channel;

    public ApiResultEmailWorker(
        ILogger<ApiResultEmailWorker> logger,
        IConfiguration configuration,
        IRabbitMqConnectionService rabbitMqService,
        IEmailSender emailSender)
    {
        _logger = logger;
        _configuration = configuration;
        _rabbitMqService = rabbitMqService;
        _emailSender = emailSender;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ApiResultEmailWorker starting...");

        await Task.Delay(5000, stoppingToken);

        try
        {
            var connection = _rabbitMqService.CreateConnection();
            _channel = connection.CreateModel();

            var exchangeName = _configuration["RabbitMQ:Exchanges:ApiResults"] ?? "api.results.exchange";
            var queueName = _configuration["RabbitMQ:Queues:ApiResultsEmail"] ?? "api.results.email.queue";

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

                    if (message != null && !string.IsNullOrEmpty(message.UserEmail))
                    {
                        await ProcessApiResultEmail(message);
                    }

                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing API result email");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            
            _logger.LogInformation("ApiResultEmailWorker listening on queue: {Queue}", queueName);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in ApiResultEmailWorker");
            throw;
        }
    }

    private async Task ProcessApiResultEmail(ApiResultMessage message)
    {
        try
        {
            string subject = $"Wyniki zapytania {message.ApiType} API - Zadanie {message.TaskId}";
            string body;

            if (message.Success)
            {
                body = $@"
<html>
<head>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; 
            background: #fff; 
            color: #000; 
            line-height: 1.6; 
            padding: 20px;
        }}
        .container {{ 
            max-width: 600px; 
            margin: 0 auto; 
            border: 1px solid #000;
        }}
        .header {{ 
            background: #000; 
            color: #fff; 
            padding: 30px; 
            border-bottom: 1px solid #000;
        }}
        .header h1 {{ 
            font-size: 24px; 
            font-weight: 600; 
            margin: 0;
        }}
        .content {{ 
            padding: 30px; 
            background: #fff;
        }}
        .info-row {{ 
            margin-bottom: 15px; 
            padding-bottom: 15px; 
            border-bottom: 1px solid #e0e0e0;
        }}
        .info-row:last-child {{ 
            border-bottom: none; 
        }}
        .info-label {{ 
            font-weight: 600; 
            margin-bottom: 5px;
        }}
        .info-value {{ 
            color: #333;
        }}
        .result-box {{ 
            margin-top: 30px; 
            border: 1px solid #000; 
            background: #f9f9f9;
        }}
        .result-header {{ 
            padding: 15px; 
            background: #000; 
            color: #fff; 
            font-weight: 600; 
            border-bottom: 1px solid #000;
        }}
        .result-content {{ 
            padding: 20px;
        }}
        pre {{ 
            background: #fff; 
            padding: 15px; 
            border: 1px solid #e0e0e0; 
            overflow-x: auto; 
            font-size: 12px; 
            line-height: 1.4; 
            white-space: pre-wrap; 
            word-wrap: break-word;
        }}
        .footer {{ 
            padding: 20px 30px; 
            background: #f0f0f0; 
            border-top: 1px solid #000; 
            font-size: 12px; 
            color: #666;
        }}
        .status-badge {{ 
            display: inline-block; 
            padding: 5px 10px; 
            background: #000; 
            color: #fff; 
            font-size: 12px; 
            font-weight: 500; 
            border: 1px solid #000;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✓ Zapytanie zakończone sukcesem</h1>
        </div>
        <div class='content'>
            <div class='info-row'>
                <div class='info-label'>ID Zadania</div>
                <div class='info-value'>{message.TaskId}</div>
            </div>
            <div class='info-row'>
                <div class='info-label'>Typ API</div>
                <div class='info-value'>{message.ApiType}</div>
            </div>
            <div class='info-row'>
                <div class='info-label'>Status</div>
                <div class='info-value'><span class='status-badge'>SUKCES</span></div>
            </div>
            
            <div class='result-box'>
                <div class='result-header'>Wyniki zapytania</div>
                <div class='result-content'>
                    <pre>{FormatJsonData(message.Data)}</pre>
                </div>
            </div>
        </div>
        <div class='footer'>
            Ten email został wygenerowany automatycznie. Nie odpowiadaj na tę wiadomość.
        </div>
    </div>
</body>
</html>";
            }
            else
            {
                body = $@"
<html>
<head>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; 
            background: #fff; 
            color: #000; 
            line-height: 1.6; 
            padding: 20px;
        }}
        .container {{ 
            max-width: 600px; 
            margin: 0 auto; 
            border: 1px solid #000;
        }}
        .header {{ 
            background: #000; 
            color: #fff; 
            padding: 30px; 
            border-bottom: 1px solid #000;
        }}
        .header h1 {{ 
            font-size: 24px; 
            font-weight: 600; 
            margin: 0;
        }}
        .content {{ 
            padding: 30px; 
            background: #fff;
        }}
        .info-row {{ 
            margin-bottom: 15px; 
            padding-bottom: 15px; 
            border-bottom: 1px solid #e0e0e0;
        }}
        .info-row:last-child {{ 
            border-bottom: none; 
        }}
        .info-label {{ 
            font-weight: 600; 
            margin-bottom: 5px;
        }}
        .info-value {{ 
            color: #333;
        }}
        .error-box {{ 
            margin-top: 30px; 
            border: 1px solid #d00; 
            background: #fee;
        }}
        .error-header {{ 
            padding: 15px; 
            background: #d00; 
            color: #fff; 
            font-weight: 600; 
            border-bottom: 1px solid #d00;
        }}
        .error-content {{ 
            padding: 20px; 
            color: #d00;
        }}
        .footer {{ 
            padding: 20px 30px; 
            background: #f0f0f0; 
            border-top: 1px solid #000; 
            font-size: 12px; 
            color: #666;
        }}
        .status-badge {{ 
            display: inline-block; 
            padding: 5px 10px; 
            background: #d00; 
            color: #fff; 
            font-size: 12px; 
            font-weight: 500; 
            border: 1px solid #d00;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✗ Błąd zapytania</h1>
        </div>
        <div class='content'>
            <div class='info-row'>
                <div class='info-label'>ID Zadania</div>
                <div class='info-value'>{message.TaskId}</div>
            </div>
            <div class='info-row'>
                <div class='info-label'>Typ API</div>
                <div class='info-value'>{message.ApiType}</div>
            </div>
            <div class='info-row'>
                <div class='info-label'>Status</div>
                <div class='info-value'><span class='status-badge'>BŁĄD</span></div>
            </div>
            
            <div class='error-box'>
                <div class='error-header'>Szczegóły błędu</div>
                <div class='error-content'>
                    {message.Error}
                </div>
            </div>
        </div>
        <div class='footer'>
            Ten email został wygenerowany automatycznie. Nie odpowiadaj na tę wiadomość.
        </div>
    </div>
</body>
</html>";
            }

            await _emailSender.SendEmailAsync(message.UserEmail, subject, body);
            
            _logger.LogInformation("API result email sent successfully to {Email} for task {TaskId}", 
                message.UserEmail, message.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send API result email to {Email} for task {TaskId}", 
                message.UserEmail, message.TaskId);
            throw;
        }
    }

    private string FormatJsonData(string? jsonData)
    {
        if (string.IsNullOrEmpty(jsonData))
            return "Brak danych";

        try
        {
            var jsonDocument = JsonDocument.Parse(jsonData);
            return JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }
        catch
        {
            return jsonData;
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
