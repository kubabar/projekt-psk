using MySql.Data.MySqlClient;

namespace Backend.Services;

public interface IUserService
{
    Task<string?> GetUserEmailAsync(string userId);
}

public class UserService : IUserService
{
    private readonly string _connectionString;
    private readonly ILogger<UserService> _logger;

    public UserService(IConfiguration configuration, ILogger<UserService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not found");
        _logger = logger;
    }

    public async Task<string?> GetUserEmailAsync(string userId)
    {
        try
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("SELECT email FROM users WHERE user_id = @userId", conn);
            cmd.Parameters.AddWithValue("@userId", userId);

            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting email for user {userId}");
            return null;
        }
    }
}
