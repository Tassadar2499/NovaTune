using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for efficient refresh token lookups.
/// </summary>
public class RefreshTokens_ByUserAndHash : AbstractIndexCreationTask<RefreshToken>
{
    public RefreshTokens_ByUserAndHash()
    {
        Map = tokens => from token in tokens
                        select new
                        {
                            token.UserId,
                            token.TokenHash,
                            token.IsRevoked,
                            token.ExpiresAt
                        };
    }
}
