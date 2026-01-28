namespace Backend.Models;

// Request DTOs
public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string IpAddress { get; set; }
    public required string UserAgent { get; set; }
}

public class VerifyCodeRequest
{
    public required string SessionId { get; set; }
    public required string Code { get; set; }
}

public class ResendCodeRequest
{
    public required string SessionId { get; set; }
    public required string Email { get; set; }
}

public class RegisterRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class ChangePasswordRequest
{
    public required string OldPassword { get; set; }
    public required string NewPassword { get; set; }
}

public class RequestPasswordResetRequest
{
    public required string Email { get; set; }
    public required string IpAddress { get; set; }
}

public class ResetPasswordRequest
{
    public required string Email { get; set; }
    public required string Token { get; set; }
    public required string NewPassword { get; set; }
}

public class GetCurrencyRateRequest
{
    public required string CurrencyCode { get; set; }
}

public class GetCompanyInfoRequest
{
    public required string Nip { get; set; }
}

// Response DTOs
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<T> ErrorResponse(string error)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Error = error
        };
    }
}

public class LoginResponse
{
    public required string SessionId { get; set; }
    public int UserId { get; set; }
    public bool PasswordExpired { get; set; }
}

public class VerifyCodeResponse
{
    public required string Token { get; set; }
    public int UserId { get; set; }
}

public class TaskAcceptedResponse
{
    public required string TaskId { get; set; }
    public required string Message { get; set; }
}

public class CurrencyRateResponse
{
    public required string CurrencyCode { get; set; }
    public double Rate { get; set; }
    public DateTime Date { get; set; }
}

public class CompanyInfo
{
    public required string Nazwa { get; set; }
    public required string Nip { get; set; }
    public required string Adres { get; set; }
    public required string Miejscowosc { get; set; }
}
