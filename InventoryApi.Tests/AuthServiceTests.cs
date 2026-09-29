using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using InventoryApi.DTOs;
using InventoryApi.Models;
using InventoryApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace InventoryApi.Tests;

public sealed class AuthServiceTests : IDisposable
{
    private const string Issuer = "InventoryApi";
    private const string Audience = "InventoryApiClient";

    private readonly TestDatabase _db = new();
    private readonly PasswordHasher<User> _hasher = new();

    // A throwaway key generated for each test - no real secret is involved.
    private readonly string _signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public void Dispose() => _db.Dispose();

    private AuthService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = _signingKey,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
            })
            .Build();

        return new AuthService(_db.CreateContext(), _hasher, configuration, NullLogger<AuthService>.Instance);
    }

    private Task<bool> RegisterAsync(string email, string password) =>
        CreateService().RegisterAsync(new RegisterRequest { Email = email, Password = password }, CancellationToken.None);

    private Task<AuthResponse?> LoginAsync(string email, string password) =>
        CreateService().LoginAsync(new LoginRequest { Email = email, Password = password }, CancellationToken.None);

    // ---- Register ----

    [Fact]
    public async Task RegisterAsync_WithNewEmail_StoresAHashedPassword()
    {
        var registered = await RegisterAsync("new@example.com", "Password123!");

        Assert.True(registered);
        await using var context = _db.CreateContext();
        var user = await context.Users.SingleAsync();
        Assert.Equal("new@example.com", user.Email);
        Assert.NotEqual("Password123!", user.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(user, user.PasswordHash, "Password123!"));
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ReturnsFalseAndAddsNoUser()
    {
        await RegisterAsync("taken@example.com", "Password123!");

        var registered = await RegisterAsync("taken@example.com", "Different456!");

        Assert.False(registered);
        await using var context = _db.CreateContext();
        Assert.Equal(1, await context.Users.CountAsync());
    }

    // ---- Login ----

    [Fact]
    public async Task LoginAsync_WithCorrectPassword_ReturnsATokenTheApiWouldAccept()
    {
        await RegisterAsync("user@example.com", "Password123!");
        var before = DateTime.UtcNow;

        var response = await LoginAsync("user@example.com", "Password123!");

        Assert.NotNull(response);
        var handler = new JwtSecurityTokenHandler();

        // Same validation parameters as Program.cs - throws if the API would reject this token.
        handler.ValidateToken(response.Token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(_signingKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
        }, out _);

        await using var context = _db.CreateContext();
        var user = await context.Users.SingleAsync();
        var jwt = handler.ReadJwtToken(response.Token);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal("user@example.com", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.InRange(response.ExpiresAt, before.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
    }

    [Fact]
    public async Task LoginAsync_WrongPasswordAndUnknownEmail_FailTheSameWay()
    {
        await RegisterAsync("user@example.com", "Password123!");

        var wrongPassword = await LoginAsync("user@example.com", "WrongPassword!");
        var unknownEmail = await LoginAsync("nobody@example.com", "Password123!");

        // Identical results, so a caller can't use login to find out which emails are registered.
        Assert.Null(wrongPassword);
        Assert.Null(unknownEmail);
    }
}
