using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

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
        // Connection string format from Aspire: "URL=http://host:port;Database=dbname"
        var connectionString = builder.Configuration.GetConnectionString("novatune");

        string ravenDbUrl;
        string ravenDbDatabase;

        if (connectionString != null && connectionString.Contains(';'))
        {
            // Parse Aspire connection string format
            var parts = connectionString.Split(';')
                .Select(p => p.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0], p => p[1]);

            ravenDbUrl = parts.GetValueOrDefault("URL") ?? "http://localhost:8080";
            ravenDbDatabase = parts.GetValueOrDefault("Database") ?? "NovaTune";
        }
        else
        {
            // Fallback to legacy configuration
            ravenDbUrl = connectionString
                ?? builder.Configuration["RavenDb:Url"]
                ?? "http://localhost:8080";
            ravenDbDatabase = builder.Configuration["RavenDb:Database"] ?? "NovaTune";
        }

        builder.Services.AddSingleton<IDocumentStore>(sp =>
        {
            var store = new DocumentStore
            {
                Urls = [ravenDbUrl],
                Database = ravenDbDatabase
            };
            store.Initialize();

            // Ensure the database exists (create if it doesn't)
            EnsureDatabaseExists(store, ravenDbDatabase);

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

    /// <summary>
    /// Ensures the specified database exists, creating it if necessary.
    /// </summary>
    private static void EnsureDatabaseExists(IDocumentStore store, string databaseName)
    {
        try
        {
            store.Maintenance.ForDatabase(databaseName).Send(new GetStatisticsOperation());
        }
        catch (DatabaseDoesNotExistException)
        {
            store.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(databaseName)));
        }
    }
}
