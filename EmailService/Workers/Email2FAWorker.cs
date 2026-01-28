using EmailService.Models;
using EmailService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace EmailService.Workers;

public class Email2FAWorker : BackgroundService
{
    private readonly ILogger<Email2FAWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRabbitMqConnectionService _rabbitMqService;
    private readonly IEmailSender _emailSender;
    private IModel? _channel;
    private readonly string _queueName;

    public Email2FAWorker(
        ILogger<Email2FAWorker> logger,
        IConfiguration configuration,
        IRabbitMqConnectionService rabbitMqService,
        IEmailSender emailSender)
    {
        _logger = logger;
        _configuration = configuration;
        _rabbitMqService = rabbitMqService;
        _emailSender = emailSender;
        _queueName = _configuration["RabbitMQ:Queues:Email2FA"] ?? "email.2fa.queue";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email2FAWorker starting...");

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
                    
                    _logger.LogInformation("Received 2FA email message: {Json}", json);

                    // Try to deserialize as Email2FAMessage first
                    Email2FAMessage? message = null;
                    PasswordResetEmailMessage? resetMessage = null;

                    try
                    {
                        var baseMessage = JsonSerializer.Deserialize<RabbitMqMessage>(json);
                        
                        if (baseMessage?.MessageType == "Email2FA")
                        {
                            message = JsonSerializer.Deserialize<Email2FAMessage>(json);
                        }
                        else if (baseMessage?.MessageType == "PasswordResetEmail")
                        {
                            resetMessage = JsonSerializer.Deserialize<PasswordResetEmailMessage>(json);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize message");
                    }

                    if (message != null)
                    {
                        await ProcessEmail2FA(message);
                    }
                    else if (resetMessage != null)
                    {
                        await ProcessPasswordResetEmail(resetMessage);
                    }
                    else
                    {
                        _logger.LogWarning("Unknown message type or failed to deserialize");
                    }

                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing 2FA email message");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
            
            _logger.LogInformation("Email2FAWorker listening on queue: {Queue}", _queueName);

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Email2FAWorker");
            throw;
        }
    }

    private async Task ProcessEmail2FA(Email2FAMessage message)
    {
        try
        {
            string subject = "Kod weryfikacyjny 2FA";
            string body = $@"
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
            text-align: center;
            border-bottom: 1px solid #000;
        }}
        .header h1 {{ 
            font-size: 24px; 
            font-weight: 600; 
            margin: 0;
        }}
        .content {{ 
            padding: 40px 30px; 
            background: #fff;
        }}
        .greeting {{ 
            font-size: 18px; 
            font-weight: 600; 
            margin-bottom: 20px;
        }}
        .code-container {{ 
            margin: 30px 0; 
            text-align: center;
        }}
        .code {{ 
            display: inline-block;
            font-size: 36px; 
            font-weight: 600; 
            letter-spacing: 8px; 
            padding: 20px 40px; 
            background: #fff; 
            border: 2px solid #000; 
            color: #000;
        }}
        .info-box {{ 
            margin: 30px 0; 
            padding: 20px; 
            border: 1px solid #000; 
            background: #f9f9f9;
        }}
        .info-title {{ 
            font-weight: 600; 
            margin-bottom: 15px;
        }}
        ul {{ 
            margin-left: 20px; 
            line-height: 2;
        }}
        li {{ 
            margin-bottom: 5px;
        }}
        .warning-box {{ 
            margin: 30px 0; 
            padding: 20px; 
            border: 1px solid #d00; 
            background: #fee; 
            color: #d00;
        }}
        .warning-title {{ 
            font-weight: 600; 
            margin-bottom: 10px;
        }}
        .footer {{ 
            padding: 20px 30px; 
            background: #f0f0f0; 
            border-top: 1px solid #000; 
            font-size: 12px; 
            color: #666; 
            text-align: center;
        }}
        .footer p {{ 
            margin: 5px 0;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Kod weryfikacyjny 2FA</h1>
        </div>
        <div class='content'>
            <div class='greeting'>Witaj!</div>
            <p>Twój kod weryfikacyjny do logowania:</p>
            
            <div class='code-container'>
                <div class='code'>{message.Code}</div>
            </div>
            
            <div class='info-box'>
                <div class='info-title'>Ważne informacje</div>
                <ul>
                    <li>Kod wygaśnie za <strong>10 minut</strong></li>
                    <li>Nie udostępniaj tego kodu nikomu</li>
                    <li>Użyj go tylko na stronie logowania</li>
                </ul>
            </div>
            
            <div class='warning-box'>
                <div class='warning-title'>Uwaga!</div>
                Jeśli to nie Ty próbowałeś się zalogować, <strong>ktoś inny zna Twoje hasło</strong>. 
                Zmień hasło natychmiast!
            </div>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomość. Nie odpowiadaj na ten email.</p>
            <p>© 2026 Auth System. Wszystkie prawa zastrzeżone.</p>
        </div>
    </div>
</body>
</html>";

            await _emailSender.SendEmailAsync(message.ToEmail, subject, body);
            
            _logger.LogInformation("2FA code sent successfully to {Email}", message.ToEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send 2FA email to {Email}", message.ToEmail);
            throw;
        }
    }

    private async Task ProcessPasswordResetEmail(PasswordResetEmailMessage message)
    {
        try
        {
            // Generate reset links
            string resetLink = $"kubabarpsk://reset-password/{message.ToEmail.Replace("@", "%40")}?token={message.Token}";
            string webLink = $"https://kubabarpsk.n2.k2-media.pl/{message.ToEmail.Replace("@", "%40")}?token={message.Token}";

            string subject = "Resetowanie hasła";
            string body = $@"
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
            text-align: center;
            border-bottom: 1px solid #000;
        }}
        .header h1 {{ 
            font-size: 24px; 
            font-weight: 600; 
            margin: 0;
        }}
        .content {{ 
            padding: 40px 30px; 
            background: #fff;
        }}
        .greeting {{ 
            font-size: 18px; 
            font-weight: 600; 
            margin-bottom: 20px;
        }}
        .button-container {{ 
            text-align: center; 
            margin: 30px 0;
        }}
        .button {{ 
            display: inline-block; 
            padding: 15px 40px; 
            background: #000; 
            color: #fff; 
            text-decoration: none; 
            border: 1px solid #000; 
            font-weight: 600; 
            font-size: 14px;
        }}
        .button:hover {{ 
            background: #333;
        }}
        .link-box {{ 
            margin: 30px 0; 
            padding: 15px; 
            background: #f0f0f0; 
            border: 1px solid #000; 
            word-break: break-all; 
            font-size: 12px; 
            font-family: monospace;
        }}
        .info-box {{ 
            margin: 30px 0; 
            padding: 20px; 
            border: 1px solid #000; 
            background: #f9f9f9;
        }}
        .info-title {{ 
            font-weight: 600; 
            margin-bottom: 15px;
        }}
        ul {{ 
            margin-left: 20px; 
            line-height: 2;
        }}
        li {{ 
            margin-bottom: 5px;
        }}
        .warning-box {{ 
            margin: 30px 0; 
            padding: 20px; 
            border: 1px solid #000; 
            background: #fff3e0; 
            color: #f57c00;
        }}
        .warning-title {{ 
            font-weight: 600; 
            margin-bottom: 10px; 
            color: #000;
        }}
        .footer {{ 
            padding: 20px 30px; 
            background: #f0f0f0; 
            border-top: 1px solid #000; 
            font-size: 12px; 
            color: #666; 
            text-align: center;
        }}
        .footer p {{ 
            margin: 5px 0;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Resetowanie hasła</h1>
        </div>
        <div class='content'>
            <div class='greeting'>Witaj!</div>
            <p>Otrzymaliśmy prośbę o zresetowanie hasła do Twojego konta.</p>
            <p>Kliknij w poniższy przycisk, aby ustawić nowe hasło:</p>
            
            <div class='button-container'>
                <a href='{webLink}' class='button'>Resetuj hasło</a>
            </div>
            
            <p>Lub skopiuj i wklej poniższy link do przeglądarki:</p>
            <div class='link-box'>{webLink}</div>
            
            <div class='info-box'>
                <div class='info-title'>Ważne informacje</div>
                <ul>
                    <li>Link wygaśnie za <strong>1 godzinę</strong></li>
                    <li>Link jest jednorazowy</li>
                    <li>Nowe hasło musi mieć minimum 8 znaków</li>
                </ul>
            </div>
            
            <div class='warning-box'>
                <div class='warning-title'>Nie prosiłeś o reset hasła?</div>
                Jeśli nie wysłałeś tej prośby, zignoruj tę wiadomość. 
                Twoje hasło pozostanie bez zmian.
            </div>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomość. Nie odpowiadaj na ten email.</p>
            <p>© 2026 Auth System. Wszystkie prawa zastrzeżone.</p>
        </div>
    </div>
</body>
</html>";

            await _emailSender.SendEmailAsync(message.ToEmail, subject, body);
            
            _logger.LogInformation("Password reset email sent successfully to {Email}", message.ToEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", message.ToEmail);
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
