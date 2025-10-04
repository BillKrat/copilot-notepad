using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotebookAI.Triples.TripleStore;
using Microsoft.Extensions.Caching.Memory;

namespace NotebookAI.Triples.Config;

public static class BookConfigProviderFactory
{
    public const string DefaultSection = "BookConfig";

    public static IServiceCollection AddBookConfigProvider(this IServiceCollection services, IConfiguration cfg, string sectionName = DefaultSection)
    {
        var section = cfg.GetSection(sectionName);
        var mode = section.GetValue<string>("Mode") ?? "TripleStore"; // or AzureAppConfig
        var useCache = section.GetValue<bool?>("Cache:Enabled") ?? true;
        var cacheSeconds = section.GetValue<int?>("Cache:TtlSeconds") ?? 60;

        if (mode.Equals("AzureAppConfig", StringComparison.OrdinalIgnoreCase))
        {
            var conn = section.GetValue<string>("ConnectionString") ?? cfg["AzureAppConfig:ConnectionString"];
            if (string.IsNullOrWhiteSpace(conn))
                throw new InvalidOperationException("AzureAppConfig connection string not configured");
            services.AddSingleton<IBookConfigProvider>(_ => new AzureAppConfigBookProvider(conn));
        }
        else
        {
            services.AddScoped<IBookConfigProvider, BookConfigFromTriplesProvider>();
        }

        if (useCache)
        {
            services.AddMemoryCache();
            services.Decorate<IBookConfigProvider>((inner, sp) => new CachedBookConfigProvider(inner, sp.GetRequiredService<IMemoryCache>(), TimeSpan.FromSeconds(cacheSeconds)));
        }
        return services;
    }

    // Simple decorator helper (since Scrutor not referenced). If Scrutor were added we could use services.Decorate directly.
    private static IServiceCollection Decorate<TService>(this IServiceCollection services, Func<TService, IServiceProvider, TService> factory) where TService : class
    {
        var descriptor = services.First(sd => sd.ServiceType == typeof(TService));
        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(TService), sp => factory((TService)sp.CreateInstance(descriptor), sp), descriptor.Lifetime));
        return services;
    }

    private static object CreateInstance(this IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance != null) return descriptor.ImplementationInstance;
        if (descriptor.ImplementationFactory != null) return descriptor.ImplementationFactory(sp);
        return ActivatorUtilities.GetServiceOrCreateInstance(sp, descriptor.ImplementationType!);
    }
}
