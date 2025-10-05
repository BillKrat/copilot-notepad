using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

internal record ProviderConfig(string Name, string Provider, string? ConnectionString, string? Mode, bool? CacheEnabled, int? CacheTtlSeconds);
internal record Auth0Config(string Domain, string ClientId, string Audience);
internal record EnvConfig(bool Production, bool UseProxy, string ApiUrl, string? ClientUrl, string? SiteUrl, Auth0Config Auth0, List<ProviderConfig> providers);

internal record Auth0ManagementConfig(string Domain, string ClientId, string ClientSecret, string TargetClientId);

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var (env, clientRoot, serverRoot, syncAuth0, verbose) = ParseArgs(args);
            var config = BuildConfiguration();

            var devCfg = BindEnv(config, "Dev");
            var prodCfg = BindEnv(config, "Prod");

            Validate(devCfg, "Dev");
            Validate(prodCfg, "Prod");

            var mgmt = BindAuth0Management(config);

            if (verbose)
            {
                Console.WriteLine($"[INFO] Loaded user-secrets.");
                Console.WriteLine($"[INFO] Client root: {clientRoot}");
                Console.WriteLine($"[INFO] Server root: {serverRoot}");
                Console.WriteLine($"[INFO] Selected env: {env}");
                Console.WriteLine($"[INFO] Auth0 Management configured: {(mgmt is null ? "no" : "yes")}");
                Console.WriteLine($"[INFO] Site URL: {devCfg.SiteUrl ?? prodCfg.SiteUrl ?? "(none)"}");
            }

            // Resolve paths
            var clientRootFull = Path.GetFullPath(clientRoot);
            var envFolder = Path.Combine(clientRootFull, "src", "environments");
            Directory.CreateDirectory(clientRootFull);
            Directory.CreateDirectory(envFolder);

            // 1) Angular environment files
            WriteFile(Path.Combine(envFolder, "environment.ts"), ToEnvironmentTs(devCfg), verbose);
            WriteFile(Path.Combine(envFolder, "environment.prod.ts"), ToEnvironmentTs(prodCfg), verbose);

            // 2) Client .env variants + active .env
            WriteFile(Path.Combine(clientRootFull, ".env.development"), ToDotEnv(devCfg, "development"), verbose);
            WriteFile(Path.Combine(clientRootFull, ".env.production"), ToDotEnv(prodCfg, "production"), verbose);

            var activeCfg = env.Equals("prod", StringComparison.OrdinalIgnoreCase) ? prodCfg : devCfg;
            var activeName = env.Equals("prod", StringComparison.OrdinalIgnoreCase) ? "production" : "development";
            WriteFile(Path.Combine(clientRootFull, ".env"), ToDotEnv(activeCfg, activeName), verbose);

            // 3) Ensure client .gitignore ignores generated files
            EnsureClientGitIgnore(clientRootFull, verbose);

            // 4) Server appsettings
            var serverRootFull = Path.GetFullPath(serverRoot);
            Directory.CreateDirectory(serverRootFull);
            UpdateServerAppSettings(serverRootFull, devCfg, prodCfg, verbose);

            // 5) Auth0 callbacks/origins derivation
            var urls = BuildAuth0UrlSets(devCfg, prodCfg, verbose);

            // 6) Optional Auth0 Management API sync
            if (syncAuth0 && mgmt is not null)
            {
                TrySyncAuth0(mgmt, urls, verbose).GetAwaiter().GetResult();
            }
            else
            {
                // Persist a helper file for manual configuration if not syncing
                var helperPath = Path.Combine(serverRootFull, "auth0-required-urls.json");
                WriteFile(helperPath, JsonSerializer.Serialize(urls, new JsonSerializerOptions { WriteIndented = true }), verbose);
                if (verbose)
                    Console.WriteLine($"[INFO] Wrote {helperPath} for manual Auth0 setup.");
            }

            Console.WriteLine("[SUCCESS] Client and server configuration generated.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] {ex.Message}");
            return 1;
        }
    }

    private static (string env, string clientRoot, string serverRoot, bool syncAuth0, bool verbose) ParseArgs(string[] args)
    {
        string env = "dev";
        string clientRoot = "../notebookai.client";
        string serverRoot = "../NotebookAI.Server";
        bool verbose = false;
        bool syncAuth0 = false;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a is "--env" && i + 1 < args.Length) env = args[++i];
            else if (a is "--client-root" && i + 1 < args.Length) clientRoot = args[++i];
            else if (a is "--server-root" && i + 1 < args.Length) serverRoot = args[++i];
            else if (a is "--sync-auth0") syncAuth0 = true;
            else if (a is "-v" or "--verbose") verbose = true;
            else if (a is "-h" or "--help")
            {
                Console.WriteLine("Usage: ProjectSetup --env [dev|prod] --client-root <path> --server-root <path> [--sync-auth0] [-v]");
                Environment.Exit(0);
            }
        }

        if (!env.Equals("dev", StringComparison.OrdinalIgnoreCase) &&
            !env.Equals("prod", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid --env. Use 'dev' or 'prod'.");
        }

        return (env.ToLowerInvariant(), clientRoot, serverRoot, syncAuth0, verbose);
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly, optional: false)
            .Build();

    private static EnvConfig BindEnv(IConfiguration cfg, string name)
    {
        var section = cfg.GetSection($"Environments:{name}");
        if (!section.Exists())
            throw new InvalidOperationException($"Missing 'Environments:{name}' section in user-secrets.");

        var auth0 = new Auth0Config(
            section.GetValue<string>("auth0:domain") ?? section.GetValue<string>("auth0:Domain") ?? "",
            section.GetValue<string>("auth0:clientId") ?? section.GetValue<string>("auth0:ClientId") ?? "",
            section.GetValue<string>("auth0:audience") ?? section.GetValue<string>("auth0:Audience") ?? ""
        );

        var providers = new List<ProviderConfig>();
        foreach (var provider in new[] { "Persistence", "TripleStore", "BookConfig" })
        {
            var sectionKey = $"{provider}";

            providers.Add(new ProviderConfig(provider,
                section.GetValue<string>($"{sectionKey}:provider") ?? "Sqlite",
                section.GetValue<string?>($"{sectionKey}:connectionString"),
                section.GetValue<string?>($"{sectionKey}:mode"),
                section.GetValue<bool?>($"{sectionKey}:cache:Enabled"),
                section.GetValue<int?>($"{sectionKey}:cache:TtlSeconds")
              ));
        }

        var production = section.GetValue("production", false);
        var useProxy = section.GetValue("useProxy", false);
        var apiUrl = section.GetValue<string>("apiUrl") ?? "";
        var clientUrl = section.GetValue<string>("clientUrl");
        var siteUrl = cfg.GetValue<string>("Ftp:site-url"); // global site-url

        return new EnvConfig(production, useProxy, apiUrl, clientUrl, siteUrl, auth0, providers);
    }

    private static Auth0ManagementConfig? BindAuth0Management(IConfiguration cfg)
    {
        var s = cfg.GetSection("Auth0Management");
        if (!s.Exists()) return null;

        var domain = s.GetValue<string>("domain") ?? s.GetValue<string>("Domain");
        var clientId = s.GetValue<string>("clientId") ?? s.GetValue<string>("ClientId");
        var clientSecret = s.GetValue<string>("clientSecret") ?? s.GetValue<string>("ClientSecret");
        var targetClientId = s.GetValue<string>("targetClientId") ?? s.GetValue<string>("TargetClientId");

        if (string.IsNullOrWhiteSpace(domain) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret) ||
            string.IsNullOrWhiteSpace(targetClientId))
        {
            return null;
        }
        return new Auth0ManagementConfig(domain, clientId, clientSecret, targetClientId);
    }

    private static void Validate(EnvConfig cfg, string name)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(cfg.ApiUrl)) missing.Add($"{name}.apiUrl");
        if (string.IsNullOrWhiteSpace(cfg.Auth0.Domain)) missing.Add($"{name}.auth0.domain");
        if (string.IsNullOrWhiteSpace(cfg.Auth0.ClientId)) missing.Add($"{name}.auth0.clientId");
        if (string.IsNullOrWhiteSpace(cfg.Auth0.Audience)) missing.Add($"{name}.auth0.audience");

        if (missing.Count > 0)
            throw new InvalidOperationException("Missing required keys in user-secrets: " + string.Join(", ", missing));
    }

    private static string ToDotEnv(EnvConfig cfg, string environmentName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {environmentName.ToUpperInvariant()} SETTINGS");
        sb.AppendLine($"API_URL={cfg.ApiUrl}");
        if (!string.IsNullOrWhiteSpace(cfg.SiteUrl)) sb.AppendLine($"SITE_URL={cfg.SiteUrl}");
        sb.AppendLine($"USE_PROXY={cfg.UseProxy.ToString().ToLowerInvariant()}");
        sb.AppendLine($"DEBUG_MODE={(cfg.Production ? "false" : "true")}");
        sb.AppendLine($"ENVIRONMENT={environmentName}");
        sb.AppendLine();
        sb.AppendLine("# Auth0 Configuration");
        sb.AppendLine($"AUTH0_DOMAIN={cfg.Auth0.Domain}");
        sb.AppendLine($"AUTH0_CLIENT_ID={cfg.Auth0.ClientId}");
        sb.AppendLine($"AUTH0_AUDIENCE={cfg.Auth0.Audience}");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string ToEnvironmentTs(EnvConfig cfg)
    {
        return
$@"export const environment = {{
  production: {cfg.Production.ToString().ToLowerInvariant()},
  useProxy: {cfg.UseProxy.ToString().ToLowerInvariant()},
  apiUrl: '{cfg.ApiUrl}',
  siteUrl: '{cfg.SiteUrl ?? cfg.ApiUrl}',
  auth0: {{
    domain: '{cfg.Auth0.Domain}',
    clientId: '{cfg.Auth0.ClientId}',
    audience: '{cfg.Auth0.Audience}',
  }}
}};
";
    }

    private static void EnsureClientGitIgnore(string clientRoot, bool verbose)
    {
        var giPath = Path.Combine(clientRoot, ".gitignore");
        var desired = new[]
        {
            "# Generated by ProjectSetup",
            ".env",
            ".env.development",
            ".env.production",
            "src/environments/environment.ts",
            "src/environments/environment.prod.ts"
        };

        var lines = File.Exists(giPath) ? File.ReadAllLines(giPath).ToList() : new List<string>();
        var changed = false;

        foreach (var d in desired)
        {
            if (!lines.Any(l => string.Equals(l.Trim(), d, StringComparison.Ordinal)))
            {
                lines.Add(d);
                changed = true;
            }
        }

        if (changed)
        {
            File.WriteAllLines(giPath, lines);
            if (verbose) Console.WriteLine($"[WRITE] {giPath} (updated ignore entries)");
        }
    }

    private static void UpdateServerAppSettings(string serverRoot, EnvConfig dev, EnvConfig prod, bool verbose)
    {
        // We update or create appsettings.Development.json and appsettings.Production.json
        var devPath = Path.Combine(serverRoot, "appsettings.Development.json");
        var prodPath = Path.Combine(serverRoot, "appsettings.Production.json");

        var devObj = new Dictionary<string, object?>
        {
            ["Auth0"] = new Dictionary<string, object?>
            {
                ["Domain"] = dev.Auth0.Domain,
                ["ClientId"] = dev.Auth0.ClientId,
                ["Audience"] = dev.Auth0.Audience
            }
        };

        var prodObj = new Dictionary<string, object?>
        {
            ["Auth0"] = new Dictionary<string, object?>
            {
                ["Domain"] = prod.Auth0.Domain,
                ["ClientId"] = prod.Auth0.ClientId,
                ["Audience"] = prod.Auth0.Audience
            }
        };

        foreach (var p in dev.providers)
        {
            var dict = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(p.Provider)) dict["Provider"] = p.Provider;
            if (!string.IsNullOrWhiteSpace(p.ConnectionString)) dict["ConnectionString"] = p.ConnectionString;
            if (!string.IsNullOrWhiteSpace(p.Mode)) dict["Mode"] = p.Mode;
            if (p.CacheEnabled.HasValue)
                dict["Cache"] = new Dictionary<string, object?>
                {
                    ["Enabled"] = p.CacheEnabled.Value,
                    ["TtlSeconds"] = p.CacheTtlSeconds ?? 300  // default TTL if not specified
                };
            devObj[p.Name] = dict;
            MergeWriteJson(devPath, dict, verbose);
        }

        foreach (var p in prod.providers)
        {
            var dict = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(p.Provider)) dict["Provider"] = p.Provider;
            if (!string.IsNullOrWhiteSpace(p.ConnectionString)) dict["ConnectionString"] = p.ConnectionString;
            if (!string.IsNullOrWhiteSpace(p.Mode)) dict["Mode"] = p.Mode;
            if (p.CacheEnabled.HasValue)
                dict["Cache"] = new Dictionary<string, object?>
                {
                    ["Enabled"] = p.CacheEnabled.Value,
                    ["TtlSeconds"] = p.CacheTtlSeconds ?? 300  // default TTL if not specified
                };
            prodObj[p.Name] = dict;
            MergeWriteJson(prodPath, dict, verbose);
        }

        MergeWriteJson(devPath, devObj, verbose);
        MergeWriteJson(prodPath, prodObj, verbose);
    }

    private static void MergeWriteJson(string path, Dictionary<string, object?> patch, bool verbose)
    {
        Dictionary<string, object?> root;
        if (File.Exists(path))
        {
            try
            {
                root = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(path)) ?? new();
            }
            catch
            {
                root = new();
            }
        }
        else root = new();

        // Shallow merge for top-level and nested dictionaries
        foreach (var kv in patch)
        {
            if (kv.Value is Dictionary<string, object?> patchChild)
            {
                if (root.TryGetValue(kv.Key, out var existing) && existing is JsonElement je && je.ValueKind == JsonValueKind.Object)
                {
                    var existingDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(je.GetRawText()) ?? new();
                    foreach (var c in patchChild) existingDict[c.Key] = c.Value;
                    root[kv.Key] = existingDict;
                }
                else if (root.TryGetValue(kv.Key, out var existingObj) && existingObj is Dictionary<string, object?> existingDict2)
                {
                    foreach (var c in patchChild) existingDict2[c.Key] = c.Value;
                }
                else
                {
                    root[kv.Key] = patchChild;
                }
            }
            else
            {
                root[kv.Key] = kv.Value;
            }
        }

        var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(false));
        if (verbose) Console.WriteLine($"[WRITE] {path}");
    }

    private static (List<string> callbacks, List<string> logoutUrls, List<string> webOrigins, List<string> allowedOrigins) BuildAuth0UrlSets(EnvConfig dev, EnvConfig prod, bool verbose)
    {
        var callbacks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var logout = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var webOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Swagger OAuth2 redirect pages for API hosts
        var swaggerDev = $"{TrimSlash(dev.ApiUrl)}/swagger/oauth2-redirect.html";
        var swaggerProd = $"{TrimSlash(prod.ApiUrl)}/swagger/oauth2-redirect.html";
        callbacks.Add(swaggerDev);
        callbacks.Add(swaggerProd);

        // If clientUrl provided, add SPA origins and callbacks
        if (!string.IsNullOrWhiteSpace(dev.ClientUrl))
        {
            callbacks.Add(TrimSlash(dev.ClientUrl)); // typical SPA uses origin as callback
            logout.Add(TrimSlash(dev.ClientUrl));
            webOrigins.Add(TrimSlash(dev.ClientUrl));
            allowedOrigins.Add(TrimSlash(dev.ClientUrl));
        }
        if (!string.IsNullOrWhiteSpace(prod.ClientUrl))
        {
            callbacks.Add(TrimSlash(prod.ClientUrl));
            logout.Add(TrimSlash(prod.ClientUrl));
            webOrigins.Add(TrimSlash(prod.ClientUrl));
            allowedOrigins.Add(TrimSlash(prod.ClientUrl));
        }

        // Optionally include API origins in webOrigins for Swagger-hosted UI flows
        webOrigins.Add(BaseOrigin(dev.ApiUrl));
        webOrigins.Add(BaseOrigin(prod.ApiUrl));

        if (verbose)
        {
            Console.WriteLine("[INFO] Auth0 URL sets prepared:");
            Console.WriteLine("  - callbacks:");
            foreach (var c in callbacks) Console.WriteLine($"    * {c}");
            Console.WriteLine("  - logout URLs:");
            foreach (var l in logout) Console.WriteLine($"    * {l}");
            Console.WriteLine("  - web origins:");
            foreach (var w in webOrigins) Console.WriteLine($"    * {w}");
            Console.WriteLine("  - allowed origins:");
            foreach (var o in allowedOrigins) Console.WriteLine($"    * {o}");
        }

        return (callbacks.ToList(), logout.ToList(), webOrigins.ToList(), allowedOrigins.ToList());
    }

    private static async Task TrySyncAuth0(Auth0ManagementConfig mgmt, (List<string> callbacks, List<string> logoutUrls, List<string> webOrigins, List<string> allowedOrigins) urls, bool verbose)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"https://{mgmt.Domain}/") };
            var token = await GetManagementTokenAsync(http, mgmt, verbose);

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("[WARN] Could not obtain Auth0 Management API token. Skipping sync.");
                return;
            }

            using var req = new HttpRequestMessage(HttpMethod.Patch, $"api/v2/clients/{Uri.EscapeDataString(mgmt.TargetClientId)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var body = new
            {
                callbacks = urls.callbacks,
                allowed_logout_urls = urls.logoutUrls,
                web_origins = urls.webOrigins,
                allowed_origins = urls.allowedOrigins
            };

            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var content = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"[WARN] Auth0 sync failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Response: {content}");
                return;
            }

            Console.WriteLine("[SUCCESS] Auth0 application settings synced (callbacks/logout/origins).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Auth0 sync error: {ex.Message}");
        }
    }

    private static async Task<string?> GetManagementTokenAsync(HttpClient http, Auth0ManagementConfig mgmt, bool verbose)
    {
        var payload = new
        {
            client_id = mgmt.ClientId,
            client_secret = mgmt.ClientSecret,
            audience = $"https://{mgmt.Domain}/api/v2/",
            grant_type = "client_credentials"
        };

        var resp = await http.PostAsync("oauth/token",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        if (!resp.IsSuccessStatusCode)
        {
            if (verbose) Console.WriteLine($"[WARN] Failed to obtain management token: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            return null;
        }

        using var s = await resp.Content.ReadAsStreamAsync();
        var doc = await JsonDocument.ParseAsync(s);
        return doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
    }

    private static void WriteFile(string path, string content, bool verbose)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (verbose) Console.WriteLine($"[WRITE] {path}");
    }

    private static string TrimSlash(string url) => url.TrimEnd('/');

    private static string BaseOrigin(string url)
    {
        var u = new Uri(url);
        var builder = new UriBuilder(u.Scheme, u.Host, u.IsDefaultPort ? -1 : u.Port);
        return builder.Uri.ToString().TrimEnd('/');
    }
}
