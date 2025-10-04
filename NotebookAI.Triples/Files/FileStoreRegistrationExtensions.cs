using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotebookAI.Triples.TripleStore;

namespace NotebookAI.Triples.Files;

public static class FileStoreRegistrationExtensions
{
    public static IServiceCollection AddFileStore(this IServiceCollection services, IConfiguration cfg, string sectionName = "FileStore")
    {
        var section = cfg.GetSection(sectionName);
        var mode = section.GetValue<string>("Mode") ?? "TripleStore"; // or AzureFileShare
        if (mode.Equals("AzureFileShare", StringComparison.OrdinalIgnoreCase))
        {
            var cs = section.GetValue<string>("ConnectionString") ?? cfg["AzureFileShare:ConnectionString"];
            var share = section.GetValue<string>("ShareName") ?? "notebookfiles";
            if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Azure File Share connection string missing");
            services.AddSingleton<IFileStore>(_ => new AzureFileShareFileStore(cs, share));
        }
        else
        {
            services.AddScoped<IFileStore, TripleStoreFileStore>();
        }
        return services;
    }
}
