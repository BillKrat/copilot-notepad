using Microsoft.Extensions.Options;
using NotebookAI.ProjectSetup.Configuration;

public class ConfigurationService
{
    private readonly Dictionary<string, EnvironmentConfig> _environments;

    public ConfigurationService(IOptions<Dictionary<string, EnvironmentConfig>> config)
    {
        ArgumentNullException.ThrowIfNull(config.Value);
        _environments = config.Value;
    }

    public void PrintProdUrl()
    {
        if (!_environments.TryGetValue("Prod", out var prod) || string.IsNullOrWhiteSpace(prod?.ApiUrl))
        {
            Console.WriteLine("Prod environment not configured.");
            return;
        }
        Console.WriteLine($"Prod API URL: {prod.ApiUrl}");
    }

    public void PrintDevUrl()
    {
        if (!_environments.TryGetValue("Dev", out var dev) || string.IsNullOrWhiteSpace(dev?.ApiUrl))
        {
            Console.WriteLine("Dev environment not configured.");
            return;
        }
        Console.WriteLine($"Dev API URL: {dev.ApiUrl}");
    }
}