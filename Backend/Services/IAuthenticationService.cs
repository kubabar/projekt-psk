namespace Backend.Services;

public interface IAuthenticationService
{
    bool IsValidEmail(string email);
    (bool Success, string? ErrorMessage, string? SessionId, int UserId, bool PasswordExpired) LoginStep1_VerifyPassword(
        string email, string password, string ipAddress, string userAgent);
    bool LoginStep2_SendCode(string sessionId, string email, out string errorMessage);
    bool LoginStep3_VerifyCode(string sessionId, string code, out string errorMessage);
    bool RegisterUser(string email, string password, out string errorMessage);
    bool ChangePassword(int userId, string oldPassword, string newPassword, out string errorMessage);
    bool RequestPasswordReset(string email, string ipAddress, out string errorMessage);
    bool ResetPasswordWithToken(string email, string token, string newPassword, out string errorMessage);
}
