using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NotebookAI.Triples.TripleStore;

public static class TripleStoreRegistrationExtensions
{
    public static IServiceCollection AddTripleStore(this IServiceCollection services, IConfiguration cfg, string sectionName = "TripleStore")
    {
        var section = cfg.GetSection(sectionName);
        var provider = section.GetValue<string>("Provider") ?? "Sqlite";
        var cs = section.GetValue<string>("ConnectionString") ?? "Data Source=triples.db";

        services.AddPooledDbContextFactory<TripleDbContext>(opts =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                opts.UseSqlServer(cs); //, b => b.MigrationsAssembly(typeof(TripleDbContext).Assembly.FullName));
            }
            else
            {
                var name = typeof(TripleDbContext).Assembly.FullName;
                opts.UseSqlite(cs); //, b => b.MigrationsAssembly(typeof(TripleDbContext).Assembly.FullName));
            }
        });

        services.AddScoped<ITripleStore, EfTripleStore>();
        return services;
    }
}
