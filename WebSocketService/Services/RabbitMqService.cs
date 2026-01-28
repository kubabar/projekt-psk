using RabbitMQ.Client;

namespace WebSocketService.Services;

public interface IRabbitMqService
{
    IConnection CreateConnection();
}

public class RabbitMqService : IRabbitMqService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqService> _logger;
    private IConnection? _connection;

    public RabbitMqService(IConfiguration configuration, ILogger<RabbitMqService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public IConnection CreateConnection()
    {
        if (_connection != null && _connection.IsOpen)
        {
            return _connection;
        }

        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "rabbitmq",
            Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest",
            VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        int maxRetries = 10;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                _connection = factory.CreateConnection();
                _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", factory.HostName, factory.Port);
                return _connection;
            }
            catch (Exception ex)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Failed to connect to RabbitMQ after {Retries} retries", maxRetries);
                    throw;
                }

                int delayMs = (int)Math.Pow(2, retryCount) * 1000;
                _logger.LogWarning("Failed to connect to RabbitMQ. Retry {Retry}/{MaxRetries} in {Delay}ms...",
                    retryCount, maxRetries, delayMs);
                Thread.Sleep(delayMs);
            }
        }

        throw new Exception("Failed to establish RabbitMQ connection");
    }

    public void Dispose()
    {
        if (_connection != null && _connection.IsOpen)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}
