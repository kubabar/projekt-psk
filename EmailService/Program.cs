using EmailService.Services;
using EmailService.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Register services
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<IRabbitMqConnectionService, RabbitMqConnectionService>();

// Register workers
builder.Services.AddHostedService<Email2FAWorker>();
builder.Services.AddHostedService<EmailNotificationsWorker>();
builder.Services.AddHostedService<ApiResultEmailWorker>();

var host = builder.Build();

Console.WriteLine("Email Service started. Waiting for messages...");
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

host.Run();
