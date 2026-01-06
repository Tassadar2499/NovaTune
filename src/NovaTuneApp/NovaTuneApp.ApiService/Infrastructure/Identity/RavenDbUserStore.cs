using Microsoft.AspNetCore.Identity;
using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Infrastructure.Identity;

/// <summary>
/// ASP.NET Identity user store backed by RavenDB.
/// Implements IUserStore, IUserPasswordStore, IUserRoleStore, and IUserEmailStore.
/// </summary>
public class RavenDbUserStore :
    IUserStore<ApplicationUser>,
    IUserPasswordStore<ApplicationUser>,
    IUserRoleStore<ApplicationUser>,
    IUserEmailStore<ApplicationUser>
{
    private readonly IAsyncDocumentSession _session;

    public RavenDbUserStore(IAsyncDocumentSession session)
    {
        _session = session;
    }

    // ========================================================================
    // IUserStore<ApplicationUser>
    // ========================================================================

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct)
    {
        user.UserId = Ulid.NewUlid().ToString();
        // Use UserId as the document ID for efficient lookups by external ID
        user.Id = $"ApplicationUsers/{user.UserId}";
        await _session.StoreAsync(user, user.Id, ct);
        await _session.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct)
    {
        // Direct document load by ID is O(1) vs querying
        return await _session.LoadAsync<ApplicationUser>($"ApplicationUsers/{userId}", ct);
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
    {
        return await _session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == normalizedUserName)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct)
    {
        await _session.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct)
    {
        _session.Delete(user);
        await _session.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult(user.UserId);

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult<string?>(user.Email);

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken ct)
    {
        user.Email = userName!;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult<string?>(user.NormalizedEmail);

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken ct)
    {
        user.NormalizedEmail = normalizedName!;
        return Task.CompletedTask;
    }

    // ========================================================================
    // IUserPasswordStore<ApplicationUser>
    // ========================================================================

    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken ct)
    {
        user.PasswordHash = passwordHash!;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult<string?>(user.PasswordHash);

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

    // ========================================================================
    // IUserRoleStore<ApplicationUser>
    // ========================================================================

    public Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken ct)
    {
        if (!user.Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            user.Roles.Add(roleName);
        return Task.CompletedTask;
    }

    public Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken ct)
    {
        user.Roles.RemoveAll(r => r.Equals(roleName, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult<IList<string>>(user.Roles);

    public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken ct) =>
        Task.FromResult(user.Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase));

    public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken ct)
    {
        return await _session.Query<ApplicationUser>()
            .Where(u => u.Roles.Contains(roleName))
            .ToListAsync(ct);
    }

    // ========================================================================
    // IUserEmailStore<ApplicationUser>
    // ========================================================================

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken ct)
    {
        user.Email = email!;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult<string?>(user.Email);

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult(true); // Email confirmation not required for MVP

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken ct) =>
        Task.CompletedTask;

    public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        return await _session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .FirstOrDefaultAsync(ct);
    }

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult<string?>(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken ct)
    {
        user.NormalizedEmail = normalizedEmail!;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Session is managed by DI container
    }
}
