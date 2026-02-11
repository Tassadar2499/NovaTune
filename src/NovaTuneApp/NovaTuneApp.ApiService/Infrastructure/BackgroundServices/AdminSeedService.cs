using Microsoft.AspNetCore.Identity;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace NovaTuneApp.ApiService.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that seeds an initial admin user on startup.
/// Configured via the "AdminSeed" configuration section.
/// </summary>
public class AdminSeedService : BackgroundService
{
    private readonly IDocumentStore _documentStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminSeedService> _logger;

    private static readonly string[] AdminRoles = ["Listener", "Admin"];
    private static readonly string[] AdminPermissions = ["audit.read"];

    public AdminSeedService(
        IDocumentStore documentStore,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AdminSeedService> logger)
    {
        _documentStore = documentStore;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var email = _configuration["AdminSeed:Email"];
        var password = _configuration["AdminSeed:Password"];
        var displayName = _configuration["AdminSeed:DisplayName"] ?? "Admin";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("AdminSeed configuration is incomplete, skipping admin seed");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
            using var session = _documentStore.OpenAsyncSession();
            var normalizedEmail = email.ToUpperInvariant();

            var existingUser = await session.Query<ApplicationUser>()
                .Customize(x => x.WaitForNonStaleResults())
                .Where(u => u.NormalizedEmail == normalizedEmail)
                .FirstOrDefaultAsync(stoppingToken);

            if (existingUser != null)
            {
                var updated = EnsureRolesAndPermissions(existingUser);
                if (updated)
                {
                    await session.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Admin seed user {Email} updated with missing roles/permissions", email);
                }
                else
                {
                    _logger.LogInformation("Admin seed user {Email} already exists with correct roles/permissions", email);
                }
                return;
            }

            var user = new ApplicationUser
            {
                UserId = Ulid.NewUlid().ToString(),
                Email = email,
                NormalizedEmail = normalizedEmail,
                DisplayName = displayName,
                Status = UserStatus.Active,
                Roles = [..AdminRoles],
                Permissions = [..AdminPermissions]
            };

            user.Id = $"ApplicationUsers/{user.UserId}";
            user.PasswordHash = passwordHasher.HashPassword(user, password);

            await session.StoreAsync(user, user.Id, stoppingToken);
            await session.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("Admin seed user created: {Email} ({UserId})", email, user.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed admin user");
        }
    }

    private static bool EnsureRolesAndPermissions(ApplicationUser user)
    {
        var updated = false;

        foreach (var role in AdminRoles)
        {
            if (!user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                user.Roles.Add(role);
                updated = true;
            }
        }

        foreach (var permission in AdminPermissions)
        {
            if (!user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
            {
                user.Permissions.Add(permission);
                updated = true;
            }
        }

        if (user.Status != UserStatus.Active)
        {
            user.Status = UserStatus.Active;
            updated = true;
        }

        return updated;
    }
}
