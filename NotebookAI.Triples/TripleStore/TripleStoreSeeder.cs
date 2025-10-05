using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotebookAI.Triples.TripleStore
{
    public sealed class TripleStoreSeeder : IHostedService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<TripleStoreSeeder> _logger;

        public TripleStoreSeeder(IServiceProvider sp, ILogger<TripleStoreSeeder> logger)
        {
            _sp = sp; _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _sp.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TripleDbContext>>();
            await using var ctx = await dbFactory.CreateDbContextAsync(cancellationToken);

            // Ensure SQLite database file / schema exists (stopgap - no migrations)
            await ctx.Database.EnsureCreatedAsync(cancellationToken);

            var store = scope.ServiceProvider.GetRequiredService<ITripleStore>();
            var count = await store.CountAsync(cancellationToken);
            if (count == 0)
            {
                _logger.LogInformation("Seeding triple store ontology ({} triples)...", NotebookAI.Triples.Ontology.BookOntologySeed.Triples.Length);
                foreach (var (s, p, o, d, dt) in NotebookAI.Triples.Ontology.BookOntologySeed.Triples)
                {
                    await store.CreateAsync(s, p, o, d, dt, cancellationToken);
                }
                var newCount = await store.CountAsync(cancellationToken);
                _logger.LogInformation("Triple store seeded with {Count} triples", newCount);
            }
            else
            {
                _logger.LogDebug("Triple store already contains {Count} triples - seeding skipped", count);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
