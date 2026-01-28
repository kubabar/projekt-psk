using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthenticationService authService, 
        IJwtService jwtService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        if (!_authService.IsValidEmail(request.Email))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Nieprawidłowy format adresu email"));
        }

        if (_authService.RegisterUser(request.Email, request.Password, out string errorMessage))
        {
            return Ok(ApiResponse<object>.SuccessResponse(null, "Użytkownik zarejestrowany pomyślnie"));
        }

        return BadRequest(ApiResponse<object>.ErrorResponse(errorMessage));
    }

    /// <summary>
    /// Step 1: Verify email and password
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var result = _authService.LoginStep1_VerifyPassword(
            request.Email, 
            request.Password, 
            request.IpAddress, 
            request.UserAgent);

        if (!result.Success)
        {
            if (result.PasswordExpired)
            {
                return Ok(ApiResponse<LoginResponse>.ErrorResponse("Hasło wygasło. Musisz je zmienić."));
            }
            return Unauthorized(ApiResponse<LoginResponse>.ErrorResponse(result.ErrorMessage ?? "Nieprawidłowy email lub hasło"));
        }

        var response = new LoginResponse
        {
            SessionId = result.SessionId ?? string.Empty,
            UserId = result.UserId,
            PasswordExpired = result.PasswordExpired
        };

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(response, "Kod weryfikacyjny zostanie wysłany"));
    }

    /// <summary>
    /// Step 2: Send 2FA code
    /// </summary>
    [HttpPost("send-code")]
    public IActionResult SendCode([FromBody] ResendCodeRequest request)
    {
        if (_authService.LoginStep2_SendCode(request.SessionId, request.Email, out string errorMessage))
        {
            return Ok(ApiResponse<object>.SuccessResponse(null, "Kod weryfikacyjny został wysłany"));
        }

        return BadRequest(ApiResponse<object>.ErrorResponse(errorMessage));
    }

    /// <summary>
    /// Step 3: Verify 2FA code and get JWT token
    /// </summary>
    [HttpPost("verify-code")]
    public IActionResult VerifyCode([FromBody] VerifyCodeRequest request)
    {
        if (_authService.LoginStep3_VerifyCode(request.SessionId, request.Code, out string errorMessage))
        {
            // In a real implementation, we'd get the user details from the session
            // For now, we'll create a placeholder token
            var token = _jwtService.GenerateToken(1, "user@example.com");
            
            var response = new VerifyCodeResponse
            {
                Token = token,
                UserId = 1
            };

            return Ok(ApiResponse<VerifyCodeResponse>.SuccessResponse(response, "Logowanie zakończone sukcesem"));
        }

        return BadRequest(ApiResponse<object>.ErrorResponse(errorMessage));
    }

    /// <summary>
    /// Change password for authenticated user
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public IActionResult ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("Nieautoryzowany dostęp"));
        }

        if (_authService.ChangePassword(userId, request.OldPassword, request.NewPassword, out string errorMessage))
        {
            return Ok(ApiResponse<object>.SuccessResponse(null, "Hasło zostało zmienione pomyślnie"));
        }

        return BadRequest(ApiResponse<object>.ErrorResponse(errorMessage));
    }

    /// <summary>
    /// Request password reset
    /// </summary>
    [HttpPost("request-password-reset")]
    public IActionResult RequestPasswordReset([FromBody] RequestPasswordResetRequest request)
    {
        if (!_authService.IsValidEmail(request.Email))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Nieprawidłowy format adresu email"));
        }

        if (_authService.RequestPasswordReset(request.Email, request.IpAddress, out string errorMessage))
        {
            return Ok(ApiResponse<object>.SuccessResponse(
                null, 
                "Jeśli konto z tym adresem email istnieje, link do resetowania hasła został wysłany"));
        }

        return BadRequest(ApiResponse<object>.ErrorResponse(errorMessage));
    }

    /// <summary>
    /// Reset password with token
    /// </summary>
    [HttpPost("reset-password")]
    public IActionResult ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (_authService.ResetPasswordWithToken(request.Email, request.Token, request.NewPassword, out string errorMessage))
        {
            return Ok(ApiResponse<object>.SuccessResponse(null, "Hasło zostało zresetowane pomyślnie"));
        }

        return BadRequest(ApiResponse<object>.ErrorResponse(errorMessage));
    }
}
