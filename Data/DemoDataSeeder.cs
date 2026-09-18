using Microsoft.AspNetCore.Identity;
using InventoryApi.Models;

namespace InventoryApi.Data;

/// <summary>
/// Ensures a fixed demo account exists so portfolio visitors can sign in without
/// registering. Idempotent - safe to run on every startup, self-heals if the
/// account is ever deleted. Credentials are intentionally public (displayed on
/// the login page), not secrets.
/// </summary>
public static class DemoDataSeeder
{
    public const string DemoEmail = "demo@example.com";
    public const string DemoPassword = "Demo123!";

    public static void SeedDemoUser(InventoryDbContext context, IPasswordHasher<User> passwordHasher)
    {
        var exists = context.Users.Any(u => u.Email == DemoEmail);
        if (exists)
        {
            return;
        }

        var demoUser = new User
        {
            Email = DemoEmail,
            CreatedAt = DateTime.UtcNow,
        };
        demoUser.PasswordHash = passwordHasher.HashPassword(demoUser, DemoPassword);

        context.Users.Add(demoUser);
        context.SaveChanges();
    }
}
