using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using InventoryApi.Data;
using InventoryApi.DTOs;
using InventoryApi.Models;

namespace InventoryApi.Services;

public class AuthService : IAuthService
{
    private readonly InventoryDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        InventoryDbContext context,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (emailExists)
        {
            return false;
        }

        var user = new User
        {
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} registered", user.Id);

        return true;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user == null)
        {
            return null;
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        _logger.LogInformation("User {UserId} logged in", user.Id);

        return GenerateToken(user);
    }

    private AuthResponse GenerateToken(User user)
    {
        var signingKey = _configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        var expiresAt = DateTime.UtcNow.AddMinutes(30);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
        };

        var key = new SymmetricSecurityKey(Convert.FromBase64String(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt,
        };
    }
}
