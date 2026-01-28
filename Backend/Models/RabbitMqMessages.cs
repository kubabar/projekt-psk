namespace Backend.Models;

public class RabbitMqMessage
{
    public required string MessageId { get; set; }
    public DateTime Timestamp { get; set; }
    public required string MessageType { get; set; }
}

public class Email2FAMessage : RabbitMqMessage
{
    public required string ToEmail { get; set; }
    public required string Code { get; set; }
}

public class EmailNotificationMessage : RabbitMqMessage
{
    public required string ToEmail { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
}

public class PasswordResetEmailMessage : RabbitMqMessage
{
    public required string ToEmail { get; set; }
    public required string Token { get; set; }
}

public class BirApiTaskMessage
{
    public required string TaskId { get; set; }
    public required string Nip { get; set; }
    public required string UserId { get; set; }
    public DateTime RequestedAt { get; set; }
}

public class NbpApiTaskMessage
{
    public required string TaskId { get; set; }
    public required string CurrencyCode { get; set; }
    public required string UserId { get; set; }
    public DateTime RequestedAt { get; set; }
}

public class ApiResponseMessage
{
    public required string TaskId { get; set; }
    public required string UserId { get; set; }
    public bool Success { get; set; }
    public string? Data { get; set; }
    public string? Error { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class ApiResultMessage
{
    public required string TaskId { get; set; }
    public required string UserId { get; set; }
    public required string UserEmail { get; set; }
    public required string ApiType { get; set; }
    public bool Success { get; set; }
    public string? Data { get; set; }
    public string? Error { get; set; }
    public DateTime CompletedAt { get; set; }
}
