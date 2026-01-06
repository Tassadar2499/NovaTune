using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for efficient user lookup by normalized email.
/// </summary>
public class Users_ByEmail : AbstractIndexCreationTask<ApplicationUser>
{
    public Users_ByEmail()
    {
        Map = users => from user in users
                       select new { user.NormalizedEmail };
    }
}
