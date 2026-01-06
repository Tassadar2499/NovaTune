using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace NovaTuneApp.ApiService.Extensions;

/// <summary>
/// Extension methods for configuring RavenDB.
/// </summary>
public static class RavenDbExtensions
{
    /// <summary>
    /// Adds RavenDB document store and session services.
    /// </summary>
    public static IHostApplicationBuilder AddRavenDb(this IHostApplicationBuilder builder)
    {
        var ravenDbUrl = builder.Configuration.GetConnectionString("novatune")
            ?? builder.Configuration["RavenDb:Url"]
            ?? "http://localhost:8080";

        var ravenDbDatabase = builder.Configuration["RavenDb:Database"] ?? "NovaTune";

        builder.Services.AddSingleton<IDocumentStore>(sp =>
        {
            var store = new DocumentStore
            {
                Urls = [ravenDbUrl],
                Database = ravenDbDatabase
            };
            store.Initialize();
            return store;
        });

        // Register RavenDB session per request
        builder.Services.AddScoped<IAsyncDocumentSession>(sp =>
        {
            var store = sp.GetRequiredService<IDocumentStore>();
            return store.OpenAsyncSession();
        });

        return builder;
    }
}
