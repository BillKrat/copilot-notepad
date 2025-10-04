using Azure.Data.AppConfiguration;
using Azure;

namespace NotebookAI.Triples.Config;

/// <summary>
/// Reads book configuration from Azure App Configuration. Expected key pattern:
/// books:{bookId}:title, books:{bookId}:publicationDate, books:{bookId}:meta:{key}
/// Also supports listing via a prefix query on 'books:'
/// </summary>
public sealed class AzureAppConfigBookProvider : IBookConfigProvider
{
    private readonly ConfigurationClient _client;

    public AzureAppConfigBookProvider(string connectionString)
    {
        _client = new ConfigurationClient(connectionString);
    }

    public async Task<IReadOnlyList<BookConfig>> GetBooksAsync(CancellationToken ct = default)
    {
        var dict = new Dictionary<string, BookConfigBuilder>(StringComparer.OrdinalIgnoreCase);
        await foreach (var setting in _client.GetConfigurationSettingsAsync(new SettingSelector { KeyFilter = "books:*" }, ct))
        {
            Parse(setting, dict);
        }
        return dict.Values.Select(b => b.Build()).ToList();
    }

    public async Task<BookConfig?> GetBookAsync(string id, CancellationToken ct = default)
    {
        var builder = new BookConfigBuilder(id);
        // Title
        var title = await TryGetAsync($"books:{id}:title", ct);
        if (title != null) builder.Title = title;
        var pub = await TryGetAsync($"books:{id}:publicationDate", ct);
        if (pub != null && DateTime.TryParse(pub, out var dt)) builder.PublicationDate = dt;
        // Meta prefix
        await foreach (var setting in _client.GetConfigurationSettingsAsync(new SettingSelector { KeyFilter = $"books:{id}:meta:*" }, ct))
        {
            var metaKey = setting.Key.Substring($"books:{id}:meta:".Length);
            builder.Metadata[metaKey] = setting.Value;
        }
        if (builder.IsEmpty) return null;
        return builder.Build();
    }

    private async Task<string?> TryGetAsync(string key, CancellationToken ct)
    {
        try
        {
            var setting = await _client.GetConfigurationSettingAsync(key, cancellationToken: ct);
            return setting.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static void Parse(ConfigurationSetting setting, Dictionary<string, BookConfigBuilder> dict)
    {
        var key = setting.Key; // books:lotr:title
        if (!key.StartsWith("books:")) return;
        var parts = key.Split(':');
        if (parts.Length < 3) return;
        var bookId = parts[1];
        var builder = dict.TryGetValue(bookId, out var existing) ? existing : (dict[bookId] = new BookConfigBuilder(bookId));
        if (parts.Length == 3 && parts[2] == "title") builder.Title = setting.Value;
        else if (parts.Length == 3 && parts[2] == "publicationDate" && DateTime.TryParse(setting.Value, out var dt)) builder.PublicationDate = dt;
        else if (parts.Length >= 4 && parts[2] == "meta")
        {
            var metaKey = string.Join(':', parts.Skip(3));
            builder.Metadata[metaKey] = setting.Value;
        }
    }

    private sealed class BookConfigBuilder
    {
        public string Id { get; }
        public string? Title { get; set; }
        public DateTime? PublicationDate { get; set; }
        public Dictionary<string,string> Metadata { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsEmpty => Title == null && PublicationDate == null && Metadata.Count == 0;

        public BookConfigBuilder(string id) => Id = id;
        public BookConfig Build() => new(Id, Title, PublicationDate, Metadata.Count == 0 ? null : Metadata);
    }
}
