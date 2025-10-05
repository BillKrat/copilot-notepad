using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotebookAI.Services.Documents;

namespace NotebookAI.Services.Persistence;

public sealed record PersistenceOptions(string Provider, string ConnectionString);

public static class PersistenceRegistrationExtensions
{
    private const string SectionName = "Persistence";

    /// <summary>
    /// Adds the pooled DbContextFactory and registers EF backed stores. Default provider: Sqlite (file or in-memory)
    /// 
    /// Configuration example (appsettings.*):
    ///   "Persistence": { "Provider": "Sqlite", "ConnectionString": "Data Source=notebook.db" }
    /// or
    ///   "Persistence": { "Provider": "SqlServer", "ConnectionString": "Server=.;Database=NotebookAI;Trusted_Connection=True;TrustServerCertificate=True" }
    /// </summary>
    public static IServiceCollection AddNotebookPersistence(this IServiceCollection services, IConfiguration cfg)
    {
        var section = cfg.GetSection(SectionName);
        var provider = section.GetValue<string>("Provider") ?? "Sqlite";
        var cs = section.GetValue<string>("ConnectionString");

        if (string.IsNullOrWhiteSpace(cs))
        {
            // Reasonable Sqlite default (file in content root)
            cs = "Data Source=NotepadtripleStore.db";
        }

        services.AddPooledDbContextFactory<NotebookDbContext>((sp, options) =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(cs, o => o.MigrationsAssembly(typeof(NotebookDbContext).Assembly.FullName));
            }
            else
            {
                // SQLite; enable WAL for concurrency if file based
                var builder = new SqliteConnectionStringBuilder(cs);
                var fileMode = builder.DataSource != ":memory:";
                var name = typeof(NotebookDbContext).Assembly.FullName;

                options.UseSqlite(cs, o => o.MigrationsAssembly(typeof(NotebookDbContext).Assembly.FullName));
                if (fileMode)
                {
                    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                }
            }
        });

        // Replace in-memory store with EF implementation
        services.AddScoped<IBookDocumentStore, EfBookDocumentStore>();

        return services;
    }
}
