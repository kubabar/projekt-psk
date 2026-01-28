using Backend.Models;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Backend.Services;

public interface IRabbitMqService
{
    void PublishEmail2FA(string toEmail, string code);
    void PublishPasswordResetEmail(string toEmail, string token);
    void PublishEmailNotification(string toEmail, string subject, string body);
    void PublishApiResult(string taskId, string userId, string userEmail, string apiType, bool success, string? data, string? error);
    IModel CreateChannel();
}

public class RabbitMqService : IRabbitMqService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IConfiguration _configuration;
    private readonly string _notificationsExchange;
    private readonly string _apiResultsExchange;

    public RabbitMqService(IConfiguration configuration)
    {
        _configuration = configuration;
        
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"],
            Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = _configuration["RabbitMQ:Username"],
            Password = _configuration["RabbitMQ:Password"],
            VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        // Retry connection with exponential backoff
        int maxRetries = 10;
        int retryCount = 0;
        
        while (retryCount < maxRetries)
        {
            try
            {
                _connection = factory.CreateConnection();
                break;
            }
            catch (Exception)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                    throw;
                    
                int delayMs = (int)Math.Pow(2, retryCount) * 1000; // Exponential backoff
                Console.WriteLine($"Failed to connect to RabbitMQ. Retry {retryCount}/{maxRetries} in {delayMs}ms...");
                Thread.Sleep(delayMs);
            }
        }

        _notificationsExchange = _configuration["RabbitMQ:Exchanges:Notifications"] ?? "notifications.exchange";
        _apiResultsExchange = _configuration["RabbitMQ:Exchanges:ApiResults"] ?? "api.results.exchange";

        InitializeExchanges();
    }

    private void InitializeExchanges()
    {
        using var channel = _connection.CreateModel();
        
        // Declare exchanges
        channel.ExchangeDeclare(_notificationsExchange, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(_apiResultsExchange, ExchangeType.Fanout, durable: true); // FANOUT dla pub-sub!

        // Declare queues
        var email2FAQueue = _configuration["RabbitMQ:Queues:Email2FA"];
        var emailNotificationsQueue = _configuration["RabbitMQ:Queues:EmailNotifications"];
        var birApiQueue = _configuration["RabbitMQ:Queues:BirApi"];
        var nbpApiQueue = _configuration["RabbitMQ:Queues:NbpApi"];
        var apiResultsWebSocketQueue = _configuration["RabbitMQ:Queues:ApiResultsWebSocket"];
        var apiResultsEmailQueue = _configuration["RabbitMQ:Queues:ApiResultsEmail"];

        channel.QueueDeclare(email2FAQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueDeclare(emailNotificationsQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueDeclare(birApiQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueDeclare(nbpApiQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueDeclare(apiResultsWebSocketQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueDeclare(apiResultsEmailQueue, durable: true, exclusive: false, autoDelete: false);

        // Bind queues to exchanges
        var email2FAKey = _configuration["RabbitMQ:RoutingKeys:Email2FA"];
        var emailNotificationsKey = _configuration["RabbitMQ:RoutingKeys:EmailNotifications"];

        channel.QueueBind(email2FAQueue, _notificationsExchange, email2FAKey);
        channel.QueueBind(emailNotificationsQueue, _notificationsExchange, emailNotificationsKey);
        
        // Bind API results queues to fanout exchange (routing key ignored in fanout)
        channel.QueueBind(apiResultsWebSocketQueue, _apiResultsExchange, "");
        channel.QueueBind(apiResultsEmailQueue, _apiResultsExchange, "");
    }

    public IModel CreateChannel()
    {
        return _connection.CreateModel();
    }

    public void PublishEmail2FA(string toEmail, string code)
    {
        var message = new Email2FAMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            MessageType = "Email2FA",
            ToEmail = toEmail,
            Code = code
        };

        PublishMessage(_notificationsExchange, _configuration["RabbitMQ:RoutingKeys:Email2FA"] ?? "email.2fa", message);
    }

    public void PublishPasswordResetEmail(string toEmail, string token)
    {
        var message = new PasswordResetEmailMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            MessageType = "PasswordResetEmail",
            ToEmail = toEmail,
            Token = token
        };

        PublishMessage(_notificationsExchange, _configuration["RabbitMQ:RoutingKeys:Email2FA"] ?? "email.2fa", message);
    }

    public void PublishEmailNotification(string toEmail, string subject, string body)
    {
        var message = new EmailNotificationMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            MessageType = "EmailNotification",
            ToEmail = toEmail,
            Subject = subject,
            Body = body
        };

        PublishMessage(_notificationsExchange, _configuration["RabbitMQ:RoutingKeys:EmailNotifications"] ?? "email.notifications", message);
    }

    public void PublishApiResult(string taskId, string userId, string userEmail, string apiType, bool success, string? data, string? error)
    {
        var message = new ApiResultMessage
        {
            TaskId = taskId,
            UserId = userId,
            UserEmail = userEmail,
            ApiType = apiType,
            Success = success,
            Data = data,
            Error = error,
            CompletedAt = DateTime.UtcNow
        };

        // Publikuj do fanout exchange - wszyscy subskrybenci dostaną wiadomość
        PublishMessage(_apiResultsExchange, "", message);
    }

    private void PublishMessage<T>(string exchange, string routingKey, T message)
    {
        using var channel = _connection.CreateModel();
        
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";

        channel.BasicPublish(
            exchange: exchange,
            routingKey: routingKey,
            basicProperties: properties,
            body: body
        );
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
