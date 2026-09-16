using InventoryApi.DTOs;

namespace InventoryApi.Services;

public interface IAuthService
{
    /// <returns>false if an account with this email already exists</returns>
    Task<bool> RegisterAsync(RegisterRequest request, CancellationToken ct);

    /// <returns>null if the email/password combination is invalid</returns>
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct);
}
