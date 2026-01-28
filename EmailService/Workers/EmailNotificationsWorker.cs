using EmailService.Models;
using EmailService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace EmailService.Workers;

public class EmailNotificationsWorker : BackgroundService
{
    private readonly ILogger<EmailNotificationsWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRabbitMqConnectionService _rabbitMqService;
    private readonly IEmailSender _emailSender;
    private IModel? _channel;
    private readonly string _queueName;

    public EmailNotificationsWorker(
        ILogger<EmailNotificationsWorker> logger,
        IConfiguration configuration,
        IRabbitMqConnectionService rabbitMqService,
        IEmailSender emailSender)
    {
        _logger = logger;
        _configuration = configuration;
        _rabbitMqService = rabbitMqService;
        _emailSender = emailSender;
        _queueName = _configuration["RabbitMQ:Queues:EmailNotifications"] ?? "email.notifications.queue";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailNotificationsWorker starting...");

        // Wait a bit for RabbitMQ to be ready
        await Task.Delay(5000, stoppingToken);

        try
        {
            var connection = _rabbitMqService.CreateConnection();
            _channel = connection.CreateModel();
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    
                    _logger.LogInformation("Received notification email message: {Json}", json);

                    var message = JsonSerializer.Deserialize<EmailNotificationMessage>(json);

                    if (message != null)
                    {
                        await ProcessNotificationEmail(message);
                    }

                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing notification email message");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
            
            _logger.LogInformation("EmailNotificationsWorker listening on queue: {Queue}", _queueName);

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in EmailNotificationsWorker");
            throw;
        }
    }

    private async Task ProcessNotificationEmail(EmailNotificationMessage message)
    {
        try
        {
            await _emailSender.SendEmailAsync(message.ToEmail, message.Subject, message.Body);
            
            _logger.LogInformation("Notification email sent successfully to {Email} with subject: {Subject}", 
                message.ToEmail, message.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification email to {Email}", message.ToEmail);
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
