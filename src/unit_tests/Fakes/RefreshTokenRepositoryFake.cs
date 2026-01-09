using NovaTuneApp.ApiService.Infrastructure.Identity;
using NovaTuneApp.ApiService.Models.Identity;

namespace NovaTune.UnitTests.Fakes;

public class RefreshTokenRepositoryFake : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = [];

    public Func<string, string, DateTime, string?, RefreshToken>? OnCreateAsync { get; set; }
    public Func<string, RefreshToken?>? OnFindByHashAsync { get; set; }
    public Func<string, int>? OnGetActiveCountForUserAsync { get; set; }

    public Task<RefreshToken> CreateAsync(string userId, string tokenHash, DateTime expiresAt, string? deviceId, CancellationToken ct)
    {
        if (OnCreateAsync != null)
        {
            var customToken = OnCreateAsync(userId, tokenHash, expiresAt, deviceId);
            Tokens.Add(customToken);
            return Task.FromResult(customToken);
        }

        var token = new RefreshToken
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            DeviceIdentifier = deviceId,
            CreatedAt = DateTime.UtcNow
        };
        Tokens.Add(token);
        return Task.FromResult(token);
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct)
    {
        if (OnFindByHashAsync != null)
        {
            return Task.FromResult(OnFindByHashAsync(tokenHash));
        }

        var token = Tokens.FirstOrDefault(t =>
            t.TokenHash == tokenHash &&
            !t.IsRevoked &&
            t.ExpiresAt > DateTime.UtcNow);
        return Task.FromResult(token);
    }

    public Task RevokeAsync(string tokenId, CancellationToken ct)
    {
        var token = Tokens.FirstOrDefault(t => t.Id == tokenId);
        if (token != null)
        {
            token.IsRevoked = true;
        }
        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(string userId, CancellationToken ct)
    {
        foreach (var token in Tokens.Where(t => t.UserId == userId && !t.IsRevoked))
        {
            token.IsRevoked = true;
        }
        return Task.CompletedTask;
    }

    public Task<int> GetActiveCountForUserAsync(string userId, CancellationToken ct)
    {
        if (OnGetActiveCountForUserAsync != null)
        {
            return Task.FromResult(OnGetActiveCountForUserAsync(userId));
        }

        var count = Tokens.Count(t =>
            t.UserId == userId &&
            !t.IsRevoked &&
            t.ExpiresAt > DateTime.UtcNow);
        return Task.FromResult(count);
    }

    public Task RevokeOldestForUserAsync(string userId, CancellationToken ct)
    {
        var oldest = Tokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefault();

        if (oldest != null)
        {
            oldest.IsRevoked = true;
        }
        return Task.CompletedTask;
    }
}