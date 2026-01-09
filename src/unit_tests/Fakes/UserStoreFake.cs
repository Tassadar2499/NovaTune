using Microsoft.AspNetCore.Identity;
using NovaTuneApp.ApiService.Models.Identity;

namespace NovaTune.UnitTests.Fakes;

public class UserStoreFake : IUserStore<ApplicationUser>, IUserEmailStore<ApplicationUser>
{
    public Dictionary<string, ApplicationUser> Users { get; } = new();

    public Func<ApplicationUser, IdentityResult>? OnCreateAsync { get; set; }
    public Func<ApplicationUser, IdentityResult>? OnUpdateAsync { get; set; }
    public Func<ApplicationUser, IdentityResult>? OnDeleteAsync { get; set; }
    public Func<string, ApplicationUser?>? OnFindByIdAsync { get; set; }
    public Func<string, ApplicationUser?>? OnFindByEmailAsync { get; set; }

    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (OnCreateAsync != null)
        {
            return Task.FromResult(OnCreateAsync(user));
        }

        // Generate UserId if not provided (simulating what a real store would do)
        if (string.IsNullOrEmpty(user.UserId))
        {
            user.UserId = Ulid.NewUlid().ToString();
        }

        Users[user.UserId] = user;
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (OnUpdateAsync != null)
        {
            return Task.FromResult(OnUpdateAsync(user));
        }

        if (Users.ContainsKey(user.UserId))
        {
            Users[user.UserId] = user;
        }
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (OnDeleteAsync != null)
        {
            return Task.FromResult(OnDeleteAsync(user));
        }

        Users.Remove(user.UserId);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (OnFindByIdAsync != null)
        {
            return Task.FromResult(OnFindByIdAsync(userId));
        }

        Users.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        var user = Users.Values.FirstOrDefault(u =>
            string.Equals(u.NormalizedEmail, normalizedUserName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.UserId);
    }

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email);
    }

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
    {
        user.Email = userName!;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.NormalizedEmail);
    }

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedName!;
        return Task.CompletedTask;
    }

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email!;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        if (OnFindByEmailAsync != null)
        {
            return Task.FromResult(OnFindByEmailAsync(normalizedEmail));
        }

        var user = Users.Values.FirstOrDefault(u =>
            string.Equals(u.NormalizedEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.NormalizedEmail);
    }

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail!;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}