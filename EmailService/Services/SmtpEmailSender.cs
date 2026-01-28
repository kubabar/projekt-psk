using System.Net;
using System.Net.Mail;

namespace EmailService.Services;

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string body);
}

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly bool _enableSsl;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;

        _smtpHost = _configuration["Smtp:Host"] 
            ?? throw new InvalidOperationException("SMTP Host not configured");
        _smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "587");
        _smtpUsername = _configuration["Smtp:Username"] 
            ?? throw new InvalidOperationException("SMTP Username not configured");
        _smtpPassword = _configuration["Smtp:Password"] 
            ?? throw new InvalidOperationException("SMTP Password not configured");
        _fromEmail = _configuration["Smtp:FromEmail"] 
            ?? throw new InvalidOperationException("SMTP FromEmail not configured");
        _fromName = _configuration["Smtp:FromName"] ?? "Auth System";
        _enableSsl = bool.Parse(_configuration["Smtp:EnableSsl"] ?? "true");

        _logger.LogInformation("SMTP configured: {Host}:{Port} from {FromEmail}", 
            _smtpHost, _smtpPort, _fromEmail);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            // Validate email
            if (!IsValidEmail(toEmail))
            {
                _logger.LogError("Invalid recipient email address: {Email}", toEmail);
                throw new ArgumentException("Invalid recipient email address", nameof(toEmail));
            }

            using var client = new SmtpClient(_smtpHost, _smtpPort);
            client.EnableSsl = _enableSsl;
            client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            client.Timeout = 30000; // 30 seconds

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(new MailAddress(toEmail));

            _logger.LogInformation("Sending email to {ToEmail} with subject: {Subject}", 
                toEmail, subject);

            await client.SendMailAsync(mailMessage);

            _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP error while sending email to {ToEmail}: {Message}", 
                toEmail, ex.Message);
            throw new Exception($"Failed to send email via SMTP: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {ToEmail}: {Message}", 
                toEmail, ex.Message);
            throw;
        }
    }

    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var mailAddress = new MailAddress(email);
            return mailAddress.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }
}
