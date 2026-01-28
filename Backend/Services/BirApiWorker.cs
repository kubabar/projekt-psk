using Backend.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using Microsoft.Extensions.Options;
using Gus.Regon.BIR11.Proxy;
using Gus.Regon.BIR11.WebService;

namespace Backend.Services;

public class BirApiWorker : BackgroundService
{
    private readonly ILogger<BirApiWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRabbitMqService _rabbitMqService;
    private readonly IServiceProvider _serviceProvider;
    private IModel? _channel;
    private readonly string _queueName;
    private readonly string _birApiKey;
    private readonly string _birEndpoint;

    public BirApiWorker(
        ILogger<BirApiWorker> logger, 
        IConfiguration configuration,
        IRabbitMqService rabbitMqService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _rabbitMqService = rabbitMqService;
        _serviceProvider = serviceProvider;
        _queueName = _configuration["RabbitMQ:Queues:BirApi"] ?? "bir.api.queue";
        _birApiKey = _configuration["BirApi:ApiKey"] ?? throw new InvalidOperationException("BIR API Key not found");
        _birEndpoint = _configuration["BirApi:Endpoint"] ?? throw new InvalidOperationException("BIR API Endpoint not found");
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
                var message = JsonSerializer.Deserialize<BirApiTaskMessage>(json);

                if (message != null)
                {
                    _logger.LogInformation($"Processing BIR API task {message.TaskId} for NIP: {message.Nip}");
                    await ProcessBirApiTask(message);
                }

                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing BIR API task");
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ProcessBirApiTask(BirApiTaskMessage message)
    {
        string? userEmail = null;
        
        try
        {
            // Pobierz email użytkownika
            using var scope = _serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            userEmail = await userService.GetUserEmailAsync(message.UserId);
            
            var birApiConfig = new BirClientOptions
            {
                EndpointAddress = _birEndpoint,
                UserKey = _birApiKey,
            };

            var birApiOptions = Options.Create(birApiConfig);
            var birApi = new Client(birApiOptions);

            var zalogujResponse = birApi.Zaloguj();

            try
            {
                var parametryWyszukiwania = new ParametryWyszukiwania
                {
                    Nip = message.Nip
                };

                var daneSzukajPodmiotyResponse = birApi.DaneSzukajPodmioty(
                    new DaneSzukajPodmiotyRequest { pParametryWyszukiwania = parametryWyszukiwania });

                using (var reader = new StringReader(daneSzukajPodmiotyResponse.DaneSzukajPodmiotyResult))
                {
                    XmlSerializer daneSzukajSerializer = new XmlSerializer(
                        typeof(Gus.Regon.BIR11.Proxy.Models.DaneSzukajPodmioty.DaneSzukajPodmioty.root));
                    
                    var daneSzukaj = (Gus.Regon.BIR11.Proxy.Models.DaneSzukajPodmioty.DaneSzukajPodmioty.root?)
                        daneSzukajSerializer.Deserialize(reader);

                    if (daneSzukaj?.dane == null)
                    {
                        throw new Exception("Nie znaleziono danych dla podanego NIP");
                    }

                    var companies = daneSzukaj.dane.Select(d => new
                    {
                        Nazwa = d.Nazwa,
                        Nip = d.Nip,
                        Adres = FormatAddress(d),
                        Miejscowosc = $"{d.KodPocztowy} {d.Miejscowosc}"
                    }).ToList();

                    var resultJson = JsonSerializer.Serialize(companies);

                    // Jedna publikacja - wszyscy subskrybenci dostaną
                    _rabbitMqService.PublishApiResult(
                        message.TaskId,
                        message.UserId,
                        userEmail ?? "",
                        "BIR",
                        true,
                        resultJson,
                        null
                    );

                    _logger.LogInformation($"BIR API task {message.TaskId} completed successfully");
                }
            }
            catch (Exception ex)
            {
                var value2 = birApi.GetValue(new GetValueRequest 
                { 
                    Body = new GetValueRequestBody { pNazwaParametru = "KomunikatKod" } 
                });
                
                throw new Exception(value2?.Body?.GetValueResult, ex);
            }
            finally
            {
                var wylogujResponse = birApi.Wyloguj(
                    new WylogujRequest { pIdentyfikatorSesji = zalogujResponse.ZalogujResult });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in BIR API task {message.TaskId}");
            
            var errorMessage = $"Błąd podczas pobierania danych: {ex.Message}";
            
            // Jedna publikacja - wszyscy subskrybenci dostaną
            _rabbitMqService.PublishApiResult(
                message.TaskId,
                message.UserId,
                userEmail ?? "",
                "BIR",
                false,
                null,
                errorMessage
            );
        }
    }

    private string FormatAddress(Gus.Regon.BIR11.Proxy.Models.DaneSzukajPodmioty.DaneSzukajPodmioty.rootDane dana)
    {
        string adres = $"{dana.Ulica} {dana.NrNieruchomosci}";
        if (!string.IsNullOrWhiteSpace(dana.NrLokalu))
        {
            adres += $"/{dana.NrLokalu}";
        }
        return adres;
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
