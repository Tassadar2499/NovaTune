using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Infrastructure.Identity;

/// <summary>
/// RavenDB-backed repository for refresh token management.
/// Stores only SHA-256 hashes of tokens per NF-3.2.
/// </summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IAsyncDocumentSession _session;

    public RefreshTokenRepository(IAsyncDocumentSession session)
    {
        _session = session;
    }

    public async Task<RefreshToken> CreateAsync(
        string userId, string tokenHash, DateTime expiresAt,
        string? deviceId, CancellationToken ct)
    {
        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            DeviceIdentifier = deviceId
        };

        await _session.StoreAsync(token, ct);
        await _session.SaveChangesAsync(ct);
        return token;
    }

    public async Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct)
    {
        return await _session.Query<RefreshToken>()
            .Where(t =>
                t.TokenHash == tokenHash &&
                !t.IsRevoked &&
                t.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync(ct);
    }

    public async Task RevokeAsync(string tokenId, CancellationToken ct)
    {
        var token = await _session.LoadAsync<RefreshToken>(tokenId, ct);
        if (token != null)
        {
            token.IsRevoked = true;
            await _session.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeAllForUserAsync(string userId, CancellationToken ct)
    {
        var tokens = await _session.Query<RefreshToken>()
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.IsRevoked = true;

        await _session.SaveChangesAsync(ct);
    }

    public async Task<int> GetActiveCountForUserAsync(string userId, CancellationToken ct)
    {
        return await _session.Query<RefreshToken>()
            .Where(t =>
                t.UserId == userId &&
                !t.IsRevoked &&
                t.ExpiresAt > DateTime.UtcNow)
            .CountAsync(ct);
    }

    public async Task RevokeOldestForUserAsync(string userId, CancellationToken ct)
    {
        var oldest = await _session.Query<RefreshToken>()
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (oldest != null)
        {
            oldest.IsRevoked = true;
            await _session.SaveChangesAsync(ct);
        }
    }
}
