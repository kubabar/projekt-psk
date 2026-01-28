using WebSocketService.Hubs;
using WebSocketService.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add SignalR
builder.Services.AddSignalR();

// CORS - teraz wszystko idzie przez nginx reverse proxy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Services
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddHostedService<ApiResponseWorker>();

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHub<NotificationHub>("/notificationHub");

app.MapGet("/", () => "WebSocket Service is running. Connect to /notificationHub");

Console.WriteLine("WebSocket Service started on port 5000");
Console.WriteLine("SignalR Hub available at: /notificationHub");

app.Run();
