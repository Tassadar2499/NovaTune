using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for admin user list with full-text search support.
/// </summary>
public class Users_ForAdminSearch : AbstractIndexCreationTask<ApplicationUser>
{
    public Users_ForAdminSearch()
    {
        Map = users => from user in users
                       select new
                       {
                           user.UserId,
                           user.Email,
                           user.NormalizedEmail,
                           user.DisplayName,
                           user.Status,
                           user.Roles,
                           user.CreatedAt,
                           user.LastLoginAt,
                           user.TrackCount,
                           user.UsedStorageBytes,
                           SearchText = new[] { user.Email, user.DisplayName }
                       };

        Index("SearchText", FieldIndexing.Search);
        Analyze("SearchText", "StandardAnalyzer");
    }
}
