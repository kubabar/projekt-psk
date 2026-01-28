using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using MySql.Data.MySqlClient;

namespace Backend.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly string _connectionString;
    private readonly IRabbitMqService _rabbitMqService;
    private const int PASSWORD_VALIDITY_DAYS = 90;
    private const int SALT_SIZE = 32;
    private const int PASSWORD_HISTORY_CHECK_COUNT = 2;

    public AuthenticationService(IConfiguration configuration, IRabbitMqService rabbitMqService)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not found");
        _rabbitMqService = rabbitMqService;
    }

    #region Email Validation

    public bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var mailAddress = new MailAddress(email);
            if (mailAddress.Address != email.Trim())
                return false;

            int atIndex = email.IndexOf('@');
            if (atIndex < 1 || atIndex == email.Length - 1)
                return false;

            if (email.IndexOf('@', atIndex + 1) != -1)
                return false;

            string domain = email.Substring(atIndex + 1);
            if (!domain.Contains("."))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Cryptographic Helpers

    private string GenerateSalt()
    {
        byte[] saltBytes = new byte[SALT_SIZE];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        return Convert.ToBase64String(saltBytes);
    }

    private string HashWithSalt(string data, string salt)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] saltedData = Encoding.UTF8.GetBytes(data + salt);
            byte[] hash = sha256.ComputeHash(saltedData);
            return Convert.ToBase64String(hash);
        }
    }

    private string GenerateCode()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] randomBytes = new byte[4];
            rng.GetBytes(randomBytes);
            int randomNumber = Math.Abs(BitConverter.ToInt32(randomBytes, 0));
            return (randomNumber % 1000000).ToString("D6");
        }
    }

    private string GenerateToken()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] tokenBytes = new byte[32];
            rng.GetBytes(tokenBytes);
            return Convert.ToBase64String(tokenBytes);
        }
    }

    #endregion

    #region Password History Validation

    private bool IsPasswordInHistory(MySqlConnection conn, int userId, string newPassword, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            var passwordHistory = new List<(string Hash, string Salt)>();

            using (var cmd = new MySqlCommand(@"
                SELECT password_hash, password_salt
                FROM password_history
                WHERE user_id = @userId
                ORDER BY created_at DESC
                LIMIT @limit", conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@limit", PASSWORD_HISTORY_CHECK_COUNT - 1);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        passwordHistory.Add((
                            reader.GetString("password_hash"),
                            reader.GetString("password_salt")
                        ));
                    }
                }
            }

            string? currentHash = null;
            string? currentSalt = null;

            using (var cmd = new MySqlCommand(@"
                SELECT password_hash, password_salt
                FROM users
                WHERE user_id = @userId", conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        currentHash = reader.GetString("password_hash");
                        currentSalt = reader.GetString("password_salt");
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentHash) && !string.IsNullOrEmpty(currentSalt))
            {
                string testHash = HashWithSalt(newPassword, currentSalt);
                if (testHash == currentHash)
                {
                    errorMessage = "Nie można użyć aktualnego hasła";
                    return true;
                }
            }

            foreach (var entry in passwordHistory)
            {
                string testHash = HashWithSalt(newPassword, entry.Salt);
                if (testHash == entry.Hash)
                {
                    errorMessage = $"Nie można użyć jednego z {PASSWORD_HISTORY_CHECK_COUNT} ostatnich haseł";
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Błąd podczas sprawdzania historii haseł: {ex.Message}";
            return true;
        }
    }

    #endregion

    #region User Registration

    public bool RegisterUser(string email, string password, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!IsValidEmail(email))
        {
            errorMessage = "Nieprawidłowy format adresu email";
            return false;
        }

        if (password.Length < 8)
        {
            errorMessage = "Hasło musi mieć minimum 8 znaków";
            return false;
        }

        try
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM users WHERE email = @email", conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    long count = (long)cmd.ExecuteScalar();
                    if (count > 0)
                    {
                        errorMessage = "Użytkownik z tym adresem email już istnieje";
                        return false;
                    }
                }

                string salt = GenerateSalt();
                string passwordHash = HashWithSalt(password, salt);

                bool success = false;
                using (var cmd = new MySqlCommand("CALL sp_register_user(@email, @hash, @salt, @validityDays, @success, @error)", conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    cmd.Parameters.AddWithValue("@hash", passwordHash);
                    cmd.Parameters.AddWithValue("@salt", salt);
                    cmd.Parameters.AddWithValue("@validityDays", PASSWORD_VALIDITY_DAYS);
                    cmd.Parameters.Add("@success", MySqlDbType.Bit).Direction = ParameterDirection.Output;
                    cmd.Parameters.Add("@error", MySqlDbType.VarChar, 255).Direction = ParameterDirection.Output;
                    cmd.ExecuteNonQuery();

                    success = Convert.ToBoolean(cmd.Parameters["@success"].Value);
                    if (!success && cmd.Parameters["@error"].Value != DBNull.Value)
                    {
                        errorMessage = cmd.Parameters["@error"].Value.ToString() ?? "Nieznany błąd";
                    }
                }

                return success;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Błąd podczas rejestracji: {ex.Message}";
            return false;
        }
    }

    #endregion

    #region Login - Step 1: Verify Password

    public (bool Success, string? ErrorMessage, string? SessionId, int UserId, bool PasswordExpired) 
        LoginStep1_VerifyPassword(string email, string password, string ipAddress, string userAgent)
    {
        try
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                int userId = 0;
                string? storedHash = null;
                string? salt = null;
                DateTime? passwordExpiresAt = null;
                int failedAttempts = 0;
                DateTime? lockedUntil = null;

                using (var cmd = new MySqlCommand(@"
                    SELECT user_id, password_hash, password_salt, password_expires_at, 
                           failed_login_attempts, locked_until
                    FROM users
                    WHERE email = @email", conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return (false, "Nieprawidłowy email lub hasło", null, 0, false);
                        }

                        userId = reader.GetInt32("user_id");
                        storedHash = reader.GetString("password_hash");
                        salt = reader.GetString("password_salt");
                        passwordExpiresAt = reader.IsDBNull(reader.GetOrdinal("password_expires_at"))
                            ? null
                            : reader.GetDateTime("password_expires_at");
                        failedAttempts = reader.GetInt32("failed_login_attempts");
                        lockedUntil = reader.IsDBNull(reader.GetOrdinal("locked_until"))
                            ? null
                            : reader.GetDateTime("locked_until");
                    }
                }

                if (lockedUntil.HasValue && lockedUntil.Value > DateTime.UtcNow)
                {
                    return (false, "Konto jest zablokowane. Spróbuj ponownie później.", null, userId, false);
                }

                string computedHash = HashWithSalt(password, salt);

                if (computedHash != storedHash)
                {
                    using (var cmd = new MySqlCommand(@"
                        UPDATE users 
                        SET failed_login_attempts = failed_login_attempts + 1,
                            locked_until = IF(failed_login_attempts + 1 >= 5, 
                                DATE_ADD(UTC_TIMESTAMP(), INTERVAL 15 MINUTE), NULL)
                        WHERE user_id = @userId", conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.ExecuteNonQuery();
                    }

                    return (false, "Nieprawidłowy email lub hasło", null, userId, false);
                }

                bool passwordExpired = passwordExpiresAt.HasValue && passwordExpiresAt.Value < DateTime.UtcNow;

                if (passwordExpired)
                {
                    return (false, "Hasło wygasło", null, userId, true);
                }

                string sessionId = Guid.NewGuid().ToString();
                using (var cmd = new MySqlCommand(@"
                    INSERT INTO login_sessions (session_id, user_id, ip_address, user_agent, expires_at)
                    VALUES (@sessionId, @userId, @ipAddress, @userAgent, DATE_ADD(UTC_TIMESTAMP(), INTERVAL 15 MINUTE))", conn))
                {
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@ipAddress", ipAddress);
                    cmd.Parameters.AddWithValue("@userAgent", userAgent);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new MySqlCommand(@"
                    UPDATE users 
                    SET failed_login_attempts = 0, locked_until = NULL
                    WHERE user_id = @userId", conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.ExecuteNonQuery();
                }

                return (true, null, sessionId, userId, false);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Błąd podczas logowania: {ex.Message}", null, 0, false);
        }
    }

    #endregion

    #region Login - Step 2: Send Code

    public bool LoginStep2_SendCode(string sessionId, string email, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                int userId = 0;
                using (var cmd = new MySqlCommand(@"
                    SELECT user_id 
                    FROM login_sessions 
                    WHERE session_id = @sessionId 
                        AND expires_at > UTC_TIMESTAMP() 
                        AND is_verified = FALSE", conn))
                {
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);

                    var result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        errorMessage = "Nieprawidłowa lub wygasła sesja";
                        return false;
                    }
                    userId = Convert.ToInt32(result);
                }

                string code = GenerateCode();
                string codeSalt = GenerateSalt();
                string codeHash = HashWithSalt(code, codeSalt);

                using (var cmd = new MySqlCommand(@"
                    INSERT INTO verification_codes (user_id, code_hash, code_salt, expires_at)
                    VALUES (@userId, @codeHash, @codeSalt, DATE_ADD(UTC_TIMESTAMP(), INTERVAL 10 MINUTE))", conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@codeHash", codeHash);
                    cmd.Parameters.AddWithValue("@codeSalt", codeSalt);
                    cmd.ExecuteNonQuery();
                }

                // Send code via RabbitMQ
                _rabbitMqService.PublishEmail2FA(email, code);

                return true;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Błąd podczas wysyłania kodu: {ex.Message}";
            return false;
        }
    }

    #endregion

    #region Login - Step 3: Verify Code

    public bool LoginStep3_VerifyCode(string sessionId, string code, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                int userId = 0;
                using (var cmd = new MySqlCommand(@"
                    SELECT user_id 
                    FROM login_sessions 
                    WHERE session_id = @sessionId 
                        AND expires_at > UTC_TIMESTAMP() 
                        AND is_verified = FALSE", conn))
                {
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);

                    var result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        errorMessage = "Nieprawidłowa lub wygasła sesja";
                        return false;
                    }
                    userId = Convert.ToInt32(result);
                }

                int? validCodeId = null;
                using (var cmd = new MySqlCommand(@"
                    SELECT code_id, code_hash, code_salt
                    FROM verification_codes
                    WHERE user_id = @userId
                        AND is_used = FALSE
                        AND expires_at > UTC_TIMESTAMP()
                    ORDER BY created_at DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int codeId = reader.GetInt32("code_id");
                            string storedHash = reader.GetString("code_hash");
                            string salt = reader.GetString("code_salt");

                            string computedHash = HashWithSalt(code, salt);
                            if (computedHash == storedHash)
                            {
                                validCodeId = codeId;
                                break;
                            }
                        }
                    }
                }

                if (!validCodeId.HasValue)
                {
                    errorMessage = "Nieprawidłowy kod weryfikacyjny";
                    return false;
                }

                using (var cmd = new MySqlCommand(@"
                    UPDATE verification_codes 
                    SET is_used = TRUE 
                    WHERE code_id = @codeId", conn))
                {
                    cmd.Parameters.AddWithValue("@codeId", validCodeId.Value);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new MySqlCommand(@"
                    UPDATE login_sessions 
                    SET is_verified = TRUE, 
                        expires_at = DATE_ADD(UTC_TIMESTAMP(), INTERVAL 24 HOUR)
                    WHERE session_id = @sessionId", conn))
                {
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    cmd.ExecuteNonQuery();
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Błąd podczas weryfikacji kodu: {ex.Message}";
            return false;
        }
    }

    #endregion

    #region Change Password

    public bool ChangePassword(int userId, string oldPassword, string newPassword, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (newPassword.Length < 8)
        {
            errorMessage = "Hasło musi mieć minimum 8 znaków";
            return false;
        }

        try
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                string? storedHash = null;
                string? salt = null;

                using (var cmd = new MySqlCommand(@"
                    SELECT password_hash, password_salt 
                    FROM users 
                    WHERE user_id = @userId", conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            errorMessage = "Użytkownik nie istnieje";
                            return false;
                        }

                        storedHash = reader.GetString("password_hash");
                        salt = reader.GetString("password_salt");
                    }
                }

                string computedHash = HashWithSalt(oldPassword, salt);
                if (computedHash != storedHash)
                {
                    errorMessage = "Nieprawidłowe stare hasło";
                    return false;
                }

                if (IsPasswordInHistory(conn, userId, newPassword, out string historyError))
                {
                    errorMessage = historyError;
                    return false;
                }

                string newSalt = GenerateSalt();
                string newPasswordHash = HashWithSalt(newPassword, newSalt);

                bool success = false;
                using (var cmd = new MySqlCommand("CALL sp_change_password(@userId, @newHash, @newSalt, @validityDays, @success, @error)", conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@newHash", newPasswordHash);
                    cmd.Parameters.AddWithValue("@newSalt", newSalt);
                    cmd.Parameters.AddWithValue("@validityDays", PASSWORD_VALIDITY_DAYS);
                    cmd.Parameters.Add("@success", MySqlDbType.Bit).Direction = ParameterDirection.Output;
                    cmd.Parameters.Add("@error", MySqlDbType.VarChar, 255).Direction = ParameterDirection.Output;
                    cmd.ExecuteNonQuery();

                    success = Convert.ToBoolean(cmd.Parameters["@success"].Value);
                    if (!success && cmd.Parameters["@error"].Value != DBNull.Value)
                    {
                        errorMessage = cmd.Parameters["@error"].Value.ToString() ?? "Nieznany błąd";
                    }
                }

                return success;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Błąd podczas zmiany hasła: {ex.Message}";
            return false;
        }
    }

    #endregion

    #region Password Reset

    public bool RequestPasswordReset(string email, string ipAddress, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                int userId = 0;
                using (var cmd = new MySqlCommand("SELECT user_id FROM users WHERE email = @email", conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    var result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        return true; // Security: don't reveal if email exists
                    }
                    userId = Convert.ToInt32(result);
                }

                string token = GenerateToken();
                string tokenSalt = GenerateSalt();
                string tokenHash = HashWithSalt(token, tokenSalt);

                using (var cmd = new MySqlCommand(@"
                    INSERT INTO password_reset_tokens (user_id, token_hash, token_salt, ip_address, expires_at)
                    VALUES (@userId, @tokenHash, @tokenSalt, @ipAddress, DATE_ADD(UTC_TIMESTAMP(), INTERVAL 1 HOUR))", conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@tokenHash", tokenHash);
                    cmd.Parameters.AddWithValue("@tokenSalt", tokenSalt);
                    cmd.Parameters.AddWithValue("@ipAddress", ipAddress);
                    cmd.ExecuteNonQuery();
                }

                // Send reset link via RabbitMQ
                _rabbitMqService.PublishPasswordResetEmail(email, token);

                return true;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Błąd podczas żądania resetu hasła: {ex.Message}";
            return false;
        }
    }

    public bool ResetPasswordWithToken(string email, string token, string newPassword, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (newPassword.Length < 8)
        {
            errorMessage = "Hasło musi mieć minimum 8 znaków";
            return false;
        }

        try
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                int userId = 0;
                using (var cmd = new MySqlCommand("SELECT user_id FROM users WHERE email = @email", conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    var result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        errorMessage = "Nieprawidłowy email";
                        return false;
                    }
                    userId = Convert.ToInt32(result);
                }

                if (IsPasswordInHistory(conn, userId, newPassword, out string historyError))
                {
                    errorMessage = historyError;
                    return false;
                }

                int tokenId = 0;
                using (var cmd = new MySqlCommand(@"
                    SELECT token_id, token_hash, token_salt
                    FROM password_reset_tokens
                    WHERE user_id = @userId
                        AND is_used = FALSE
                        AND expires_at > UTC_TIMESTAMP()
                    ORDER BY created_at DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int currentTokenId = reader.GetInt32("token_id");
                            string storedHash = reader.GetString("token_hash");
                            string salt = reader.GetString("token_salt");
                            string tokenHash = HashWithSalt(token, salt);

                            if (tokenHash == storedHash)
                            {
                                tokenId = currentTokenId;
                                break;
                            }
                        }
                    }
                }

                if (tokenId == 0)
                {
                    errorMessage = "Nieprawidłowy lub wygasły token";
                    return false;
                }

                string newSalt = GenerateSalt();
                string newPasswordHash = HashWithSalt(newPassword, newSalt);

                bool success = false;
                using (var cmd = new MySqlCommand("CALL sp_reset_password_with_token(@tokenId, @newHash, @newSalt, @validityDays, @success, @error)", conn))
                {
                    cmd.Parameters.AddWithValue("@tokenId", tokenId);
                    cmd.Parameters.AddWithValue("@newHash", newPasswordHash);
                    cmd.Parameters.AddWithValue("@newSalt", newSalt);
                    cmd.Parameters.AddWithValue("@validityDays", PASSWORD_VALIDITY_DAYS);
                    cmd.Parameters.Add("@success", MySqlDbType.Bit).Direction = ParameterDirection.Output;
                    cmd.Parameters.Add("@error", MySqlDbType.VarChar, 255).Direction = ParameterDirection.Output;
                    cmd.ExecuteNonQuery();

                    success = Convert.ToBoolean(cmd.Parameters["@success"].Value);
                    if (!success && cmd.Parameters["@error"].Value != DBNull.Value)
                    {
                        errorMessage = cmd.Parameters["@error"].Value.ToString() ?? "Nieznany błąd";
                    }
                }

                return success;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Błąd podczas resetowania hasła: {ex.Message}";
            return false;
        }
    }

    #endregion
}
