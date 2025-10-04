using Adventures.Shared.Ftp.Client;
using Adventures.Shared.Ftp.Interfaces;
using Adventures.Shared.Ftp.Pooling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#if NET6_0_OR_GREATER && FTP_WEB_EXTENSIONS
using Microsoft.AspNetCore.Builder;
#endif

namespace Adventures.Shared.Ftp.Extensions;

public class FtpClientOptions
{
    public string Host { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Port { get; set; } = 21;
    /// <summary>Maximum pooled underlying connections (only used when Lifetime=Scoped)</summary>
    public int PoolSize { get; set; } = 8;
}

public static class FtpRegistrationExtensions
{
    private const string DefaultSection = "Ftp";

    private static IServiceCollection AddFtpCore(this IServiceCollection services, ServiceLifetime lifetime)
    {
        if (lifetime == ServiceLifetime.Scoped)
        {
            // Register a singleton pool; provide scoped wrapper instances
            services.AddSingleton<FtpClientPool>();
            services.AddScoped<IFtpClientAsync>(sp => new PooledFtpClientAsync(sp.GetRequiredService<FtpClientPool>()));
            return services;
        }

        services.Add(new ServiceDescriptor(typeof(IFtpClientAsync), sp =>
        {
            var opts = sp.GetRequiredService<IOptions<FtpClientOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opts.Host)) throw new InvalidOperationException("FtpClientOptions.Host is required");
            if (string.IsNullOrWhiteSpace(opts.Username)) throw new InvalidOperationException("FtpClientOptions.Username is required");
            if (string.IsNullOrWhiteSpace(opts.Password)) throw new InvalidOperationException("FtpClientOptions.Password is required");
            var logger = sp.GetRequiredService<ILogger<FluentFtpClientAsync>>();
            return new FluentFtpClientAsync(opts.Host, opts.Username, opts.Password, logger);
        }, lifetime));
        return services;
    }

    public static IServiceCollection AddFtp(this IServiceCollection services, Action<FtpClientOptions> configure, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        services.AddOptions<FtpClientOptions>()
            .Configure(configure)
            .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Host is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Username), "Username is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Password), "Password is required");
        return services.AddFtpCore(lifetime);
    }

    public static IServiceCollection AddFtp(this IServiceCollection services, IConfiguration configuration, string sectionName = DefaultSection, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        var section = configuration.GetSection(sectionName);
        services.AddOptions<FtpClientOptions>()
            .Bind(section)
            .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Host is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Username), "Username is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Password), "Password is required");
        return services.AddFtpCore(lifetime);
    }

#if NET6_0_OR_GREATER && FTP_WEB_EXTENSIONS
    public static WebApplicationBuilder AddFtp(this WebApplicationBuilder builder, string sectionName = DefaultSection, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        builder.Services.AddFtp(builder.Configuration, sectionName, lifetime);
        return builder;
    }
#endif

    public static HostApplicationBuilder AddFtp(this HostApplicationBuilder builder, string sectionName = DefaultSection, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        builder.Services.AddFtp(builder.Configuration, sectionName, lifetime);
        return builder;
    }

    public static IHostBuilder AddFtp(this IHostBuilder hostBuilder, string sectionName = DefaultSection, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        hostBuilder.ConfigureServices((ctx, services) =>
        {
            services.AddFtp(ctx.Configuration, sectionName, lifetime);
        });
        return hostBuilder;
    }
}
